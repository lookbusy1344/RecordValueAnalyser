namespace RecordValueAnalyser;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CheckResultTuple = (ValueEqualityResult, string? memberName);

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RecordValueAnalyser : DiagnosticAnalyzer
{
	public const string DiagnosticId = "JSV01";

	private static readonly DiagnosticDescriptor ParamValueSemanticsRule = new(
		DiagnosticId,
		new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources)),
		new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources)),
		"Design",
		DiagnosticSeverity.Warning,
		true,
		new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources)));

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ParamValueSemanticsRule);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(AnalyzeRecordDeclaration, SyntaxKind.RecordDeclaration, SyntaxKind.RecordStructDeclaration);
	}

	private static void AnalyzeRecordDeclaration(SyntaxNodeAnalysisContext context)
	{
		var recordDeclaration = (RecordDeclarationSyntax)context.Node;

		// if the record has an Equals(T) method, then we're ok. No need to check further
		if (RecordValueSemantics.RecordHasEquals(context)) {
			return;
		}

		var recordSymbol = context.SemanticModel.GetDeclaredSymbol(recordDeclaration);
		var union = recordSymbol != null && UnionSemantics.IsUnion(recordSymbol) ? recordSymbol : null;

		// check the parameter list eg record A(int i, int j)
		var recordParams = recordDeclaration.ParameterList?.Parameters;
		if (recordParams != null) {
			foreach (var recParam in recordParams) {
				// get the type of the member, and unwrap it if it's nullable
				var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(recParam);
				var type = parameterSymbol?.Type;
				if (type == null) {
					continue;
				}

				// if the property has value semantics, then we're ok
				var (result, errorMember) = RecordValueSemantics.CheckMember(type);
				if (result == ValueEqualityResult.Ok) {
					continue;
				}

				// otherwise, we have a problem. show a diagnostic
				var typeName = type?.ToDisplayString(NullableFlowState.None) ?? "";
				var memberName = recParam.Identifier.ValueText;
				var args = FormatMember(typeName, memberName, result, errorMember);

				var diagnostic = Diagnostic.Create(ParamValueSemanticsRule, recParam.GetLocation(), args);
				context.ReportDiagnostic(diagnostic);
			}
		}

		// check any fields and properties
		foreach (var member in recordDeclaration.Members) {
			if (member is FieldDeclarationSyntax fieldDeclaration) {
				// A field declaration can declare multiple variables: int[] A, B, C;
				// Get the shared type and check it once, then report each failing variable separately.
				var fieldType = context.SemanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type).Type;
				if (fieldType == null) {
					continue;
				}

				var fieldCheck = RecordValueSemantics.CheckMember(fieldType);
				var fieldTypeName = fieldType.ToDisplayString(NullableFlowState.None);
				foreach (var variable in fieldDeclaration.Declaration.Variables) {
					var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable);
					if (fieldSymbol != null && !RecordValueSemantics.IsComparedMember(fieldSymbol)) {
						continue; // static fields are not compared
					}

					var (fieldResult, fieldErrorMember) = CheckUnionMember(union, fieldSymbol) ?? fieldCheck;
					if (fieldResult == ValueEqualityResult.Ok) {
						continue;
					}

					var args = FormatMember(fieldTypeName, variable.Identifier.ValueText, fieldResult, fieldErrorMember);
					context.ReportDiagnostic(Diagnostic.Create(ParamValueSemanticsRule, fieldDeclaration.GetLocation(), args));
				}
			} else {
				var (unwrappedType, memberName, _) = RecordValueSemantics.GetPropertyOrFieldUnderlyingType(context, member);
				if (unwrappedType == null) {
					continue;
				}

				var memberSymbol = context.SemanticModel.GetDeclaredSymbol(member);
				if (memberSymbol != null && !RecordValueSemantics.IsComparedMember(memberSymbol)) {
					continue; // computed and static properties are not compared
				}

				// if the property has value semantics, then we're ok
				var (result, errorMember) = CheckUnionMember(union, memberSymbol) ?? RecordValueSemantics.CheckMember(unwrappedType);
				if (result == ValueEqualityResult.Ok) {
					continue;
				}

				// otherwise, we have a problem. show a diagnostic
				var typeName = unwrappedType.ToDisplayString(NullableFlowState.None);
				var args = FormatMember(typeName, memberName ?? "?", result, errorMember);

				context.ReportDiagnostic(Diagnostic.Create(ParamValueSemanticsRule, member.GetLocation(), args));
			}
		}
	}

	/// <summary>
	///     A record that is a union type holds its case in an object member, compared with object.Equals, so that member is
	///     checked through the case types. Returns null for any other member, or when the record is not a union type.
	/// </summary>
	private static CheckResultTuple? CheckUnionMember(INamedTypeSymbol? union, ISymbol? member)
	{
		if (union == null || member == null || !UnionSemantics.HoldsCase(union, member)) {
			return null;
		}

		return UnionSemantics.FirstFailingCase(union) is ITypeSymbol failingCase
			? (ValueEqualityResult.CaseFailed, failingCase.ToDisplayString(NullableFlowState.None))
			: (ValueEqualityResult.Ok, null);
	}

	/// <summary>
	///     The diagnostic argument, eg "S X (field int[])" or "object _value (case int[])"
	/// </summary>
	private static string FormatMember(string typeName, string memberName, ValueEqualityResult result, string? errorMember) =>
		errorMember == null
			? $"{typeName} {memberName}"
			: $"{typeName} {memberName} ({(result == ValueEqualityResult.CaseFailed ? "case" : "field")} {errorMember})";
}
