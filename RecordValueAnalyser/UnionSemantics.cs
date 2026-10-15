namespace RecordValueAnalyser;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

/// <summary>
///     Value semantics of C# 15 union types.
///     https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md
/// </summary>
internal static class UnionSemantics
{
	private const string ValuePropertyName = "Value";
	private const string MembersInterfaceName = "IUnionMembers";
	private const string FactoryMethodName = "Create";

	/// <summary>
	///     True if this is a union, declared with 'union' or marked with UnionAttribute.
	///     Roslyn 4.8 has no union support, so the legacy build sees no unions.
	/// </summary>
#if LEGACY_ROSLYN
	internal static bool IsUnion(ITypeSymbol type) => false;
#else
#pragma warning disable RSEXPERIMENTAL006 // ITypeSymbol.IsUnion is flagged experimental in Roslyn 5.9.0
	internal static bool IsUnion(ITypeSymbol type) => type.IsUnion;
#pragma warning restore RSEXPERIMENTAL006
#endif

	/// <summary>
	///     True if this is a union type that is a struct. A union type that is a class compares by reference, and a record
	///     that is a union type is checked where it is declared.
	/// </summary>
	internal static bool IsStructUnionType(ITypeSymbol type) => IsUnion(type) && type.TypeKind == TypeKind.Struct;

	/// <summary>
	///     A union stores its case in a field compared with object.Equals, so the case types stand in for that field.
	///     Every other instance field is checked as for any struct.
	/// </summary>
	internal static IEnumerable<ITypeSymbol> GetMemberTypes(INamedTypeSymbol union)
	{
		var storage = GetStorage(union);
		var otherFields = RecordValueSemantics.GetInstanceFields(union)
			.Where(f => !storage.Contains(f, SymbolEqualityComparer.Default))
			.Select(f => f.Type);

		return GetCaseTypes(union).Concat(otherFields);
	}

	/// <summary>
	///     True if this member of a record that is a union type holds the case: a storage field, or the auto-property Value behind one
	/// </summary>
	internal static bool HoldsCase(INamedTypeSymbol union, ISymbol member) =>
		GetStorage(union).Any(f => SymbolEqualityComparer.Default.Equals(f, member)
								   || SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, member));

	/// <summary>
	///     The first case type without value semantics, or null when every case type has them
	/// </summary>
	internal static ITypeSymbol? FirstFailingCase(INamedTypeSymbol union) =>
		GetCaseTypes(union).FirstOrDefault(t => RecordValueSemantics.CheckMember(t).Item1 != ValueEqualityResult.Ok);

	/// <summary>
	///     The instance fields holding the case: the backing field of an auto-property Value, otherwise the only reference-typed
	///     field that every case type converts to. With several such fields the storage is ambiguous, so none is treated as storage.
	/// </summary>
	private static IReadOnlyList<IFieldSymbol> GetStorage(INamedTypeSymbol union)
	{
		var fields = RecordValueSemantics.GetInstanceFields(union).ToList();
		var autoValue = fields.Where(f => f.AssociatedSymbol is IPropertySymbol p && IsValueProperty(p)).ToList();
		if (autoValue.Count > 0) {
			return autoValue;
		}

		var caseTypes = GetCaseTypes(union).ToList();
		var candidates = fields.Where(f => f.Type.IsReferenceType && caseTypes.All(c => ConvertsTo(c, f.Type))).ToList();

		return caseTypes.Count > 0 && candidates.Count == 1 ? candidates : [];
	}

	/// <summary>
	///     True if a value of the case type can be stored in the target type: object, the type itself, a base class or an interface
	/// </summary>
	private static bool ConvertsTo(ITypeSymbol caseType, ITypeSymbol target)
	{
		var source = RecordValueSemantics.UnwrapNullable(caseType);
		return RecordValueSemantics.IsObjectType(target)
			   || BaseTypesAndSelf(source).Contains(target, SymbolEqualityComparer.Default)
			   || source.AllInterfaces.Contains(target, SymbolEqualityComparer.Default);
	}

	private static IEnumerable<ITypeSymbol> BaseTypesAndSelf(ITypeSymbol type)
	{
		for (var current = type; current != null; current = current.BaseType) {
			yield return current;
		}
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
