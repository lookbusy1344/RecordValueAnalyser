using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace RecordValueAnalyser;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RecordValueAnalyser : DiagnosticAnalyzer
{
	public const string DiagnosticId = "JSV01";

	private static readonly DiagnosticDescriptor ParamValueSemanticsRule = new(DiagnosticId, "Value semantics warning",
		"Member '{0}' does not have value semantics", "Design", DiagnosticSeverity.Warning, isEnabledByDefault: true,
		description: "Member '{0}' does not have value semantics.");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ParamValueSemanticsRule];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(AnalyzeRecordDeclaration, SyntaxKind.RecordDeclaration, SyntaxKind.RecordStructDeclaration);
	}

	private static void AnalyzeRecordDeclaration(SyntaxNodeAnalysisContext context)
	{
		var recordDeclaration = (RecordDeclarationSyntax)context.Node;
		//var recordTypeSymbol = context.SemanticModel.GetDeclaredSymbol(recordDeclaration);

		// if the record has an Equals(T) method, then we're ok. No need to check further
		if (RecordValueSemantics.RecordHasEquals(context)) return;

		// check the parameter list eg record A(int i, int j)
		var recordParams = recordDeclaration.ParameterList?.Parameters;
		if (recordParams != null)
			foreach (var recParam in recordParams)
			{
				// get the type of the member, and unwrap it if it's nullable
				var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(recParam);
				var type = parameterSymbol?.Type;
				if (type == null) continue;

				// if the property has value semantics, then we're ok
				var (result, errorMember) = RecordValueSemantics.CheckMember(type);
				if (result == ValueEqualityResult.Ok) continue;

				// otherwise, we have a problem. show a diagnostic
				var typeName = type?.ToDisplayString(NullableFlowState.None) ?? "";
				var memberName = recParam.Identifier.ValueText;
				var args = errorMember == null ? $"{typeName} {memberName}" : $"{typeName} {memberName} (field {errorMember})";

				var diagnostic = Diagnostic.Create(ParamValueSemanticsRule, recParam.GetLocation(), args);
				context.ReportDiagnostic(diagnostic);
			}

		// check any fields and properties
		foreach (var member in recordDeclaration.Members)
		{
			var (unwrappedType, memberName, _) = RecordValueSemantics.GetPropertyOrFieldUnderlyingType(context, member);
			if (unwrappedType == null) continue;

			// if the property has value semantics, then we're ok
			var (result, errorMember) = RecordValueSemantics.CheckMember(unwrappedType);
			if (result == ValueEqualityResult.Ok) continue;

			// otherwise, we have a problem. show a diagnostic
			var typeName = unwrappedType?.ToDisplayString(NullableFlowState.None) ?? "";
			memberName ??= "?";
			var args = errorMember == null ? $"{typeName} {memberName}" : $"{typeName} {memberName} ({errorMember})";

			var diagnostic = Diagnostic.Create(ParamValueSemanticsRule, member.GetLocation(), args);
			context.ReportDiagnostic(diagnostic);
		}
	}
}
