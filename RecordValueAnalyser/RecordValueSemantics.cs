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
	CaseFailed,
}

internal static class RecordValueSemantics
{
	/// <summary>
	///     BCL structs whose Equals compares identity (an array or span reference), not contents
	/// </summary>
	private static readonly HashSet<string> ReferenceEqualityStructs = new(System.StringComparer.Ordinal) {
		"System.Collections.Immutable.ImmutableArray<T>",
		"System.ArraySegment<T>",
		"System.Memory<T>",
		"System.ReadOnlyMemory<T>",
	};

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

		// Cycle detection: a circular struct layout (CS0523), or a union reached again through its case types, must not cause
		// unbounded recursion. Track only the current recursion path; remove on exit so sibling branches are not skipped.
		// A back-edge adds no new member types, so it passes; any failing type on the cycle is reported where it is checked.
		if (!visited.Add(type)) {
			return (ValueEqualityResult.Ok, null);
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

			if (IsReferenceEqualityStruct(type)) {
				return (ValueEqualityResult.Failed, null); // ImmutableArray<T>, ArraySegment<T>, Memory<T> etc.
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
				INamedTypeSymbol union when UnionSemantics.IsStructUnionType(union) => UnionSemantics.GetMemberTypes(union),
				_ when IsStruct(type) => GetInstanceFields(type).Select(f => f.Type),
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
	///     Given Nullable&lt;T&gt;, return T. Otherwise return the type itself.
	/// </summary>
	private static ITypeSymbol? GetUnderlyingType(ITypeSymbol? type) => type == null ? null : UnwrapNullable(type);

	/// <summary>
	///     Given Nullable&lt;T&gt;, return T. Otherwise return the type itself. A substituted 'T?' with T = int is an annotated int,
	///     not Nullable&lt;int&gt;, and is returned as it is.
	/// </summary>
	internal static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
		type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ? nullable.TypeArguments[0] : type;

	/// <summary>
	///     Gets the generic name of type eg System.Collections.Immutable.ImmutableArray&lt;T&gt;
	/// </summary>
	private static string? GetGenericName(ITypeSymbol? typeSymbol) => typeSymbol?.OriginalDefinition.ToDisplayString();

	/// <summary>
	///     Check for a class
	/// </summary>
	private static bool IsClassType(ITypeSymbol? type) => type?.TypeKind == TypeKind.Class;

	/// <summary>
	///     Check for System.Object
	/// </summary>
	internal static bool IsObjectType(ITypeSymbol? type) => type?.SpecialType == SpecialType.System_Object;

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
	///     The instance fields of a type, including auto-property backing fields. ValueType.Equals compares these and nothing else.
	/// </summary>
	internal static IEnumerable<IFieldSymbol> GetInstanceFields(ITypeSymbol type) =>
		type.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic);

	/// <summary>
	///     True if record and struct equality compare this member: an instance field, or an instance property with a backing field
	/// </summary>
	internal static bool IsComparedMember(ISymbol member) =>
		member switch {
			IFieldSymbol field => !field.IsStatic,
			IPropertySymbol property => GetInstanceFields(property.ContainingType)
				.Any(f => SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, property)),
			_ => false,
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
	///     Is this a BCL struct whose Equals compares identity rather than contents?
	/// </summary>
	private static bool IsReferenceEqualityStruct(ITypeSymbol type) =>
		GetGenericName(type) is string name && ReferenceEqualityStructs.Contains(name);
}
