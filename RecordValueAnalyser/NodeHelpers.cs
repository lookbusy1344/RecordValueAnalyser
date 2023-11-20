using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

// https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.itypesymbol?view=roslyn-dotnet-4.6.0

namespace RecordValueAnalyser;

internal enum ValueEqualityResult
{
	Ok,
	Failed,
	NestedFailed
}

internal static class NodeHelpers
{
	/// <summary>
	/// Wrapper around the various equality checks
	/// </summary>
	internal static (ValueEqualityResult, string? membername) ValueEqualityWrapper(ITypeSymbol? type)
	{
		type = NodeHelpers.GetUnderlyingType(type); // unwrap any nullable
		if (type == null) return (ValueEqualityResult.Ok, null);

		if (IsStrictlyInvalid(type)) return (ValueEqualityResult.Failed, null);      // object and dynamic
		if (HasSimpleEquality(type)) return (ValueEqualityResult.Ok, null);       // primitive types, string, enum
		if (IsInlineArray(type)) return (ValueEqualityResult.Failed, null);      // Inline array structs lack value semantics

		if (!type.IsTupleType)
		{
			// for tuples we ignore Equals(T) and Equals(object)
			if (HasEqualsTMethod(type)) return (ValueEqualityResult.Ok, null);        // Equals(T) not inherited
			if (HasEqualsObjectMethod(type)) return (ValueEqualityResult.Ok, null);   // Equals(object) overridden and not inherited

			if (IsRecordType(type)) return (ValueEqualityResult.Ok, null);            // a record is ok
			if (IsClassType(type)) return (ValueEqualityResult.Failed, null);         // a class is not ok
		}

		// get the members of the tuple or struct
		IEnumerable<ISymbol>? members = null;
		if (type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsTupleType)
			members = namedTypeSymbol.TupleElements;
		else if (IsStruct(type))
			members = type.GetMembers();

		if (members != null)
		{
			// this compound type has members. Check each one
			foreach (var member in members)
			{
				var memberType = member switch
				{
					IPropertySymbol property => property.Type,
					IFieldSymbol field => field.Type,
					_ => null,
				};

				if (memberType == null) continue;
				var (result, _) = ValueEqualityWrapper(memberType);

				if (result != ValueEqualityResult.Ok)
				{
					// if the nested type fails, return the type name
					var nestedtypefail = memberType?.ToDisplayString(NullableFlowState.None) ?? "UNKNOWN";
					return (ValueEqualityResult.NestedFailed, nestedtypefail);
				}
			}

			return (ValueEqualityResult.Ok, null); // compound type passes equality test
		}

		// by default, fail
		return (ValueEqualityResult.Failed, null);
	}

	/// <summary>
	/// Does this record have an Equals(T) method?
	/// </summary>
	internal static bool RecordHasEquals(SyntaxNodeAnalysisContext context)
	{
		var recordDeclaration = (RecordDeclarationSyntax)context.Node;
		var recordTypeSymbol = context.SemanticModel.GetDeclaredSymbol(recordDeclaration);

		foreach (var member in recordDeclaration.Members)
		{
			var memberSymbol = context.SemanticModel.GetDeclaredSymbol(member);

			if (memberSymbol is IMethodSymbol methodSymbol)
			{
				// this is a method member. Check if its Equals(T), and if so no further checks are needed
				if (methodSymbol.Name == "Equals"
					&& methodSymbol.ReturnType.SpecialType == SpecialType.System_Boolean
					&& methodSymbol.Parameters.Length == 1
					&& methodSymbol.Parameters[0].Type.Equals(recordTypeSymbol, SymbolEqualityComparer.Default)) return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Get the type and name of the property or field
	/// </summary>
	internal static (ITypeSymbol? membertype, string? membername, bool isproperty) GetPropertyOrFieldUnderlyingType(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax member)
	{
		// get the field / property type and name
		ITypeSymbol? type;
		string? memberstr;
		bool isproperty;
		if (member is PropertyDeclarationSyntax propertyDeclaration)
		{
			type = context.SemanticModel.GetTypeInfo(propertyDeclaration.Type).Type;
			memberstr = propertyDeclaration.Identifier.ValueText;
			isproperty = true;
		}
		else if (member is FieldDeclarationSyntax fieldDeclaration)
		{
			type = context.SemanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type).Type;
			memberstr = fieldDeclaration.Declaration.Variables[0].Identifier.ValueText;
			isproperty = false;
		}
		else
			return (null, null, false);

		// get the type of the member, and unwrap it if it's nullable
		return (NodeHelpers.GetUnderlyingType(type), memberstr, isproperty);
	}

	/// <summary>
	/// Given Nullable type, return the underlying type. If not nullable, return the type itself.
	/// </summary>
	internal static ITypeSymbol? GetUnderlyingType(ITypeSymbol? type)
	{
		if (type == null) return null;
		if (!IsNullableValueType(type)) return type;

		var namedtype = type as INamedTypeSymbol;
		return namedtype?.TypeArguments[0];
	}

	private static bool IsNullableValueType(ITypeSymbol? type) => type?.IsValueType == true && type.NullableAnnotation == NullableAnnotation.Annotated;

	private static bool IsClassType(ITypeSymbol? type) => type?.TypeKind == TypeKind.Class;

	private static bool IsObjectType(ITypeSymbol? type) => type?.SpecialType == SpecialType.System_Object;

	/// <summary>
	/// Record class or Record struct
	/// </summary>
	private static bool IsRecordType(ITypeSymbol? type) => type != null && (type.IsRecord || (type.TypeKind == TypeKind.Struct && type.IsReadOnly));

	/// <summary>
	/// True if this is a struct
	/// </summary>
	private static bool IsStruct(ITypeSymbol? type) => type != null && type.TypeKind == TypeKind.Struct;

	/// <summary>
	/// True if this is an inline array (new in .NET8). These lack value semantics
	/// </summary>
	private static bool IsInlineArray(ITypeSymbol? type) =>
		type != null
			&& type.TypeKind == TypeKind.Struct
			&& type.GetAttributes().Any(attribute =>
				attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InlineArrayAttribute");

	/// <summary>
	/// Is this always invalid? (object, dynamic, etc)
	/// </summary>
	private static bool IsStrictlyInvalid(ITypeSymbol? type) =>
		type?.SpecialType == SpecialType.System_Object || type?.TypeKind == TypeKind.Dynamic;

	/// <summary>
	/// Simple equality means a primitive type, string or enum
	/// </summary>
	private static bool HasSimpleEquality(ITypeSymbol? type) => type != null && (type.TypeKind == TypeKind.Enum || IsPrimitiveType(type));

	/// <summary>
	/// Is this a primitive type? Includes string
	/// </summary>
	private static bool IsPrimitiveType(ITypeSymbol? type)
	{
		return type != null
			&& type.SpecialType switch
			{
				SpecialType.System_Boolean or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Byte or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Char or SpecialType.System_String
				=> true,
				_ => false,
			};
	}

	/// <summary>
	/// Does this type have an Equals(T) method that takes a single parameter of the same type?
	/// </summary>
	private static bool HasEqualsTMethod(ITypeSymbol? type) =>
		type?.GetMembers("Equals")
			.OfType<IMethodSymbol>()
			.Any(m => m.Parameters.Length == 1
				&& m.Parameters[0].Type.Equals(type, SymbolEqualityComparer.Default)
				&& !m.IsStatic
				&& !m.IsOverride) == true;

	/// <summary>
	/// Does this type have an Equals(object) override method defined in this type?
	/// </summary>
	private static bool HasEqualsObjectMethod(ITypeSymbol? type) =>
		type?.GetMembers("Equals")
			.OfType<IMethodSymbol>()
			.Any(m => m.Parameters.Length == 1
				&& IsObjectType(m.Parameters[0].Type)
				&& !m.IsStatic
				&& m.IsOverride
				&& m.ContainingType.Equals(type, SymbolEqualityComparer.Default)) == true;
}
