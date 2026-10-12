namespace RecordValueAnalyser;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

/// <summary>
///     Value semantics of C# 15 union structs.
///     https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md
/// </summary>
internal static class UnionSemantics
{
	private const string ValuePropertyName = "Value";
	private const string MembersInterfaceName = "IUnionMembers";
	private const string FactoryMethodName = "Create";

	/// <summary>
	///     True if this is a union struct, declared with 'union' or marked with UnionAttribute. Union classes compare by reference.
	/// </summary>
#pragma warning disable RSEXPERIMENTAL006 // ITypeSymbol.IsUnion is flagged experimental in Roslyn 5.9.0
	internal static bool IsUnionStruct(ITypeSymbol type) => type is { IsUnion: true, TypeKind: TypeKind.Struct };
#pragma warning restore RSEXPERIMENTAL006

	/// <summary>
	///     A union stores its case in an object member, compared with object.Equals, so the case types stand in for that member.
	///     Every other member is checked as for any struct.
	/// </summary>
	internal static IEnumerable<ITypeSymbol> GetMemberTypes(INamedTypeSymbol union)
	{
		var storage = GetStorage(union);
		var otherMembers = RecordValueSemantics.GetStructMembers(union)
			.Where(m => !storage.Contains(m, SymbolEqualityComparer.Default))
			.Select(RecordValueSemantics.MemberType);

		return GetCaseTypes(union).Concat(otherMembers);
	}

	/// <summary>
	///     The members holding the boxed case: the Value property, and the object field behind it when Value is not an auto-property.
	///     With several object fields the storage is ambiguous, so none of them is treated as storage.
	/// </summary>
	private static IReadOnlyList<ISymbol> GetStorage(INamedTypeSymbol union)
	{
		var valueProperties = union.GetMembers().OfType<IPropertySymbol>().Where(IsValueProperty).ToList();
		var valueIsAuto = union.GetMembers().OfType<IFieldSymbol>()
			.Any(f => f.IsImplicitlyDeclared && f.AssociatedSymbol is IPropertySymbol p && IsValueProperty(p));
		var objectFields = RecordValueSemantics.GetStructMembers(union).OfType<IFieldSymbol>().Where(f => RecordValueSemantics.IsObjectType(f.Type)).ToList();

		return objectFields.Count == 1 && !valueIsAuto ? [.. valueProperties, objectFields[0]] : valueProperties;
	}

	/// <summary>
	///     The union's object-typed Value property, declared directly or as an explicit interface implementation
	/// </summary>
	private static bool IsValueProperty(IPropertySymbol property) =>
		RecordValueSemantics.IsObjectType(property.Type)
		&& (property.Name == ValuePropertyName
			|| property.ExplicitInterfaceImplementations.Any(p => p.Name == ValuePropertyName));

	/// <summary>
	///     Case types are the parameter types of the union creation members. When the union implements a nested IUnionMembers
	///     interface, those are the static Create methods found on it; otherwise the union's constructors.
	///     A constructed generic union yields substituted types.
	/// </summary>
	private static IEnumerable<ITypeSymbol> GetCaseTypes(INamedTypeSymbol union)
	{
		var provider = union.GetTypeMembers(MembersInterfaceName)
			.FirstOrDefault(i => i.TypeKind == TypeKind.Interface && union.AllInterfaces.Contains(i, SymbolEqualityComparer.Default));

		var creationMembers = provider == null
			? union.InstanceConstructors.AsEnumerable()
			: provider.AllInterfaces.Prepend(provider)
				.SelectMany(i => i.GetMembers(FactoryMethodName))
				.OfType<IMethodSymbol>()
				.Where(m => m.IsStatic && m.ReturnType.Equals(union, SymbolEqualityComparer.Default));

		return creationMembers.Where(IsCreationMember).Select(m => m.Parameters[0].Type);
	}

	/// <summary>
	///     A union creation member is public and takes one by-value or 'in' parameter
	/// </summary>
	private static bool IsCreationMember(IMethodSymbol method) =>
		method is { DeclaredAccessibility: Accessibility.Public, Parameters.Length: 1 }
		&& method.Parameters[0].RefKind is RefKind.None or RefKind.In;
}
