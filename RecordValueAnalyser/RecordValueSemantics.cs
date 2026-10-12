namespace RecordValueAnalyser;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.itypesymbol?view=roslyn-dotnet-4.6.0

// Tuples for return values
using CheckResultTuple = (ValueEqualityResult, string? memberName);
using MemberStatusTuple = (Microsoft.CodeAnalysis.ITypeSymbol? memberType, string? memberName, bool isProperty);

internal enum ValueEqualityResult
{
	Ok,
	Failed,
	NestedFailed,
}

internal static class RecordValueSemantics
{
	// C# 15 union member names
	// https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md
	private const string UnionValuePropertyName = "Value";
	private const string UnionMembersInterfaceName = "IUnionMembers";
	private const string UnionFactoryMethodName = "Create";

	/// <summary>
	///     Check if this record member type has value semantics
	/// </summary>
	internal static CheckResultTuple CheckMember(ITypeSymbol? type)
		=> CheckMember(type, [with(SymbolEqualityComparer.Default)]);

	private static CheckResultTuple CheckMember(ITypeSymbol? type, HashSet<ITypeSymbol> visited)
	{
		type = GetUnderlyingType(type); // unwrap any nullable
		if (type == null) {
			return (ValueEqualityResult.Ok, null);
		}

		// Cycle detection: a circular struct layout (CS0523) must not cause unbounded recursion.
		// Track only the current recursion path; remove on exit so sibling branches are not skipped.
		if (!visited.Add(type)) {
			return (ValueEqualityResult.Ok, null); // back-edge: compiler already reports CS0523
		}

		try {
			if (IsStrictlyInvalid(type)) {
				return (ValueEqualityResult.Failed, null); // object and dynamic
			}

			if (HasSimpleEquality(type)) {
				return (ValueEqualityResult.Ok, null); // primitive types, string, enum
			}

			// special cases
			if (IsInlineArray(type)) {
				return (ValueEqualityResult.Failed, null); // Inline array structs lack value semantics
			}

			if (IsImmutableArrayType(type)) {
				return (ValueEqualityResult.Failed, null); // ImmutableArray<T> lacks value semantics
			}

			if (IsArraySegmentType(type)) {
				return (ValueEqualityResult.Failed, null); // ArraySegment<T> compares array identity, not contents
			}

			if (IsMemoryType(type)) {
				return (ValueEqualityResult.Failed, null); // Memory<T>/ReadOnlyMemory<T> compare span identity, not contents
			}

			if (!type.IsTupleType) {
				// for tuples we ignore Equals(T) and Equals(object)
				if (HasEqualsTMethod(type)) {
					return (ValueEqualityResult.Ok, null); // Equals(T) not inherited
				}

				if (HasEqualsObjectMethod(type)) {
					return (ValueEqualityResult.Ok, null); // Equals(object) overridden and not inherited
				}

				if (IsRecordType(type)) {
					return (ValueEqualityResult.Ok, null); // a record is ok
				}

				if (IsClassType(type)) {
					return (ValueEqualityResult.Failed, null); // a class is not ok
				}
			}

			// get the member types of the tuple or struct
			IEnumerable<ITypeSymbol>? memberTypes = type switch {
				INamedTypeSymbol { IsTupleType: true } tuple => tuple.TupleElements.Select(e => e.Type),
				INamedTypeSymbol union when IsUnionStruct(union) => GetUnionMemberTypes(union),
				_ when IsStruct(type) => GetStructMembers(type).Select(MemberType),
				_ => null,
			};

			if (memberTypes != null) {
				// this compound type has members. Check each one
				foreach (var memberType in memberTypes) {
					var (result, _) = CheckMember(memberType, visited);

					if (result != ValueEqualityResult.Ok) {
						// if the nested type fails, return the type name
						return (ValueEqualityResult.NestedFailed, memberType.ToDisplayString(NullableFlowState.None));
					}
				}

				return (ValueEqualityResult.Ok, null); // compound type passes equality test
			}

			// by default, fail
			return (ValueEqualityResult.Failed, null);
		}
		finally {
			_ = visited.Remove(type);
		}
	}

	/// <summary>
	///     Does this record have a user-defined Equals(T) method?
	///     Uses the semantic type symbol so partial record declarations in other files are included.
	/// </summary>
	internal static bool RecordHasEquals(SyntaxNodeAnalysisContext context)
	{
		var recordDeclaration = (RecordDeclarationSyntax)context.Node;

		if (context.SemanticModel.GetDeclaredSymbol(recordDeclaration) is not INamedTypeSymbol recordTypeSymbol) {
			return false;
		}

		return recordTypeSymbol.GetMembers("Equals")
			.OfType<IMethodSymbol>()
			.Any(m => m.ReturnType.SpecialType == SpecialType.System_Boolean
					  && m.Parameters.Length == 1
					  && m.Parameters[0].Type.Equals(recordTypeSymbol, SymbolEqualityComparer.Default)
					  && !m.IsImplicitlyDeclared);
	}

	/// <summary>
	///     Get the type and name of the property or field
	/// </summary>
	internal static MemberStatusTuple GetPropertyOrFieldUnderlyingType(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax member)
	{
		// get the field / property type and name
		ITypeSymbol? type;
		string? memberName;
		bool isProperty;
		if (member is PropertyDeclarationSyntax propertyDeclaration) {
			type = context.SemanticModel.GetTypeInfo(propertyDeclaration.Type).Type;
			memberName = propertyDeclaration.Identifier.ValueText;
			isProperty = true;
		} else if (member is FieldDeclarationSyntax fieldDeclaration) {
			type = context.SemanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type).Type;
			if (fieldDeclaration.Declaration.Variables.Count == 0) {
				return (null, null, false);
			}

			memberName = fieldDeclaration.Declaration.Variables[0].Identifier.ValueText;
			isProperty = false;
		} else {
			return (null, null, false);
		}

		// get the type of the member, and unwrap it if it's nullable
		return (GetUnderlyingType(type), memberName, isProperty);
	}

	/// <summary>
	///     Given Nullable value type, return the underlying type. If not nullable, return the type itself.
	/// </summary>
	private static ITypeSymbol? GetUnderlyingType(ITypeSymbol? type)
	{
		if (type == null) {
			return null;
		}

		if (!IsNullableValueType(type)) {
			return type;
		}

		var namedType = type as INamedTypeSymbol;
		return namedType?.TypeArguments[0];
	}

	/// <summary>
	///     Gets the generic name of type eg System.Collections.Immutable.ImmutableArray&lt;T&gt;
	/// </summary>
	private static string? GetGenericName(ITypeSymbol? typeSymbol) => typeSymbol?.OriginalDefinition.ToDisplayString();

	/// <summary>
	///     Is this a nullable value type, like 'int?'
	/// </summary>
	private static bool IsNullableValueType(ITypeSymbol? type) =>
		type is { IsValueType: true, NullableAnnotation: NullableAnnotation.Annotated };

	/// <summary>
	///     Check for a class
	/// </summary>
	private static bool IsClassType(ITypeSymbol? type) => type?.TypeKind == TypeKind.Class;

	/// <summary>
	///     Check for System.Object
	/// </summary>
	private static bool IsObjectType(ITypeSymbol? type) => type?.SpecialType == SpecialType.System_Object;

	/// <summary>
	///     Record class or record struct. Plain readonly structs are excluded.
	/// </summary>
	private static bool IsRecordType(ITypeSymbol? type) => type?.IsRecord == true;

	/// <summary>
	///     True if this is a struct
	/// </summary>
	private static bool IsStruct(ITypeSymbol? type) => type is { TypeKind: TypeKind.Struct };

	/// <summary>
	///     True if this is an inline array (new in .NET8). These lack value semantics
	/// </summary>
	private static bool IsInlineArray(ITypeSymbol? type) =>
		type is { TypeKind: TypeKind.Struct } && type.GetAttributes().Any(attribute =>
			attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InlineArrayAttribute");

	/// <summary>
	///     True if this is a C# 15 union struct, declared with 'union' or marked with UnionAttribute. Union classes compare by reference.
	///     https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md
	/// </summary>
#pragma warning disable RSEXPERIMENTAL006 // ITypeSymbol.IsUnion is flagged experimental in Roslyn 5.9.0
	private static bool IsUnionStruct(ITypeSymbol type) => type is { IsUnion: true, TypeKind: TypeKind.Struct };
#pragma warning restore RSEXPERIMENTAL006

	/// <summary>
	///     A union stores its case in an object member, compared with object.Equals, so the case types stand in for that member.
	///     Every other member is checked as for any struct.
	/// </summary>
	private static IEnumerable<ITypeSymbol> GetUnionMemberTypes(INamedTypeSymbol union)
	{
		var storage = GetUnionStorage(union);
		var otherMembers = GetStructMembers(union)
			.Where(m => !storage.Contains(m, SymbolEqualityComparer.Default))
			.Select(MemberType);

		return GetUnionCaseTypes(union).Concat(otherMembers);
	}

	/// <summary>
	///     The members holding the boxed case: the Value property, and the object field behind it when Value is not an auto-property.
	///     With several object fields the storage is ambiguous, so none of them is treated as storage.
	/// </summary>
	private static IReadOnlyList<ISymbol> GetUnionStorage(INamedTypeSymbol union)
	{
		var valueProperties = union.GetMembers().OfType<IPropertySymbol>().Where(IsUnionValueProperty).ToList();
		var valueIsAuto = union.GetMembers().OfType<IFieldSymbol>()
			.Any(f => f.IsImplicitlyDeclared && f.AssociatedSymbol is IPropertySymbol p && IsUnionValueProperty(p));
		var objectFields = GetStructMembers(union).OfType<IFieldSymbol>().Where(f => IsObjectType(f.Type)).ToList();

		return objectFields.Count == 1 && !valueIsAuto ? [.. valueProperties, objectFields[0]] : valueProperties;
	}

	/// <summary>
	///     The union's object-typed Value property, declared directly or as an explicit interface implementation
	/// </summary>
	private static bool IsUnionValueProperty(IPropertySymbol property) =>
		IsObjectType(property.Type)
		&& (property.Name == UnionValuePropertyName
			|| property.ExplicitInterfaceImplementations.Any(p => p.Name == UnionValuePropertyName));

	/// <summary>
	///     Case types are the parameter types of the union creation members. When the union implements a nested IUnionMembers
	///     interface, those are the static Create methods found on it; otherwise the union's constructors.
	///     A constructed generic union yields substituted types.
	/// </summary>
	private static IEnumerable<ITypeSymbol> GetUnionCaseTypes(INamedTypeSymbol union)
	{
		var provider = union.GetTypeMembers(UnionMembersInterfaceName)
			.FirstOrDefault(i => i.TypeKind == TypeKind.Interface && union.AllInterfaces.Contains(i, SymbolEqualityComparer.Default));

		var creationMembers = provider == null
			? union.InstanceConstructors.AsEnumerable()
			: provider.AllInterfaces.Prepend(provider)
				.SelectMany(i => i.GetMembers(UnionFactoryMethodName))
				.OfType<IMethodSymbol>()
				.Where(m => m.IsStatic && m.ReturnType.Equals(union, SymbolEqualityComparer.Default));

		return creationMembers.Where(IsUnionCreationMember).Select(m => m.Parameters[0].Type);
	}

	/// <summary>
	///     A union creation member is public and takes one by-value or 'in' parameter
	/// </summary>
	private static bool IsUnionCreationMember(IMethodSymbol method) =>
		method is { DeclaredAccessibility: Accessibility.Public, Parameters.Length: 1 }
		&& method.Parameters[0].RefKind is RefKind.None or RefKind.In;

	/// <summary>
	///     The explicit fields and properties of a struct. Auto-property backing fields are covered by their property.
	/// </summary>
	private static IEnumerable<ISymbol> GetStructMembers(ITypeSymbol type) =>
		type.GetMembers().Where(m => m is IFieldSymbol { IsImplicitlyDeclared: false } or IPropertySymbol);

	private static ITypeSymbol MemberType(ISymbol member) =>
		member switch {
			IFieldSymbol field => field.Type,
			IPropertySymbol property => property.Type,
			_ => throw new System.ArgumentException($"Not a field or property: {member}", nameof(member)),
		};

	/// <summary>
	///     Is this always invalid? (object, dynamic, etc)
	/// </summary>
	private static bool IsStrictlyInvalid(ITypeSymbol? type) =>
		type?.SpecialType == SpecialType.System_Object || type?.TypeKind == TypeKind.Dynamic;

	/// <summary>
	///     Simple equality means a primitive type, string or enum
	/// </summary>
	private static bool HasSimpleEquality(ITypeSymbol? type) => type != null && (type.TypeKind == TypeKind.Enum || IsPrimitiveType(type));

	/// <summary>
	///     Is this a primitive type? Includes string
	/// </summary>
	private static bool IsPrimitiveType(ITypeSymbol? type) =>
		type != null
		&& type.SpecialType switch {
			SpecialType.System_Boolean or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64
				or SpecialType.System_Byte or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64
				or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal or SpecialType.System_Char
				or SpecialType.System_String
				=> true,
			_ => false,
		};

	/// <summary>
	///     Does this type have an Equals(T) method that takes a single parameter of the same type?
	/// </summary>
	private static bool HasEqualsTMethod(ITypeSymbol? type) =>
		type?.GetMembers("Equals")
			.OfType<IMethodSymbol>()
			.Any(m => m.Parameters.Length == 1
					  && m.Parameters[0].Type.Equals(type, SymbolEqualityComparer.Default)
					  && m is { IsStatic: false, IsOverride: false, IsAbstract: false }) == true;

	/// <summary>
	///     Does this type have an Equals(object) override method defined in this type?
	/// </summary>
	private static bool HasEqualsObjectMethod(ITypeSymbol? type) =>
		type?.GetMembers("Equals")
			.OfType<IMethodSymbol>()
			.Any(m => m.Parameters.Length == 1
					  && IsObjectType(m.Parameters[0].Type)
					  && m is { IsStatic: false, IsOverride: true }
					  && m.ContainingType.Equals(type, SymbolEqualityComparer.Default)) == true;

	/// <summary>
	///     Is this an immutable array? They lack value semantics
	/// </summary>
	private static bool IsImmutableArrayType(ITypeSymbol? typeSymbol) =>
		GetGenericName(typeSymbol) == "System.Collections.Immutable.ImmutableArray<T>";

	/// <summary>
	///     Is this an ArraySegment&lt;T&gt;? Its Equals compares array identity, not element contents.
	/// </summary>
	private static bool IsArraySegmentType(ITypeSymbol? typeSymbol) =>
		GetGenericName(typeSymbol) == "System.ArraySegment<T>";

	/// <summary>
	///     Is this Memory&lt;T&gt; or ReadOnlyMemory&lt;T&gt;? Their Equals compares span identity, not element contents.
	/// </summary>
	private static bool IsMemoryType(ITypeSymbol? typeSymbol) =>
		GetGenericName(typeSymbol) is "System.Memory<T>" or "System.ReadOnlyMemory<T>";
}
