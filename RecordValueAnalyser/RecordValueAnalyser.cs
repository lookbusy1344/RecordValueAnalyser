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
		//var recordTypeSymbol = context.SemanticModel.GetDeclaredSymbol(recordDeclaration);

		// if the record has an Equals(T) method, then we're ok. No need to check further
		if (NodeHelpers.RecordHasEquals(context)) return;

		// check the parameter list eg record A(int i, int j)
		var recordparams = recordDeclaration.ParameterList?.Parameters;
		if (recordparams != null)
			foreach (var recparam in recordparams)
			{
				// get the type of the member, and unwrap it if it's nullable
				var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(recparam);
				var type = parameterSymbol?.Type;
				if (type == null) continue;

				// if the property has value semantics, then we're ok
				var (result, errormember) = NodeHelpers.ValueEqualityWrapper(type);
				if (result == ValueEqualityResult.Ok) continue;

				// otherwise, we have a problem. show a diagnostic
				var typestr = type?.ToDisplayString(NullableFlowState.None) ?? "";
				var memberstr = recparam.Identifier.ValueText;
				var args = errormember == null ? $"{typestr} {memberstr}" : $"{typestr} {memberstr} (field {errormember})";

				var diagnostic = Diagnostic.Create(ParamValueSemanticsRule, recparam.GetLocation(), args);
				context.ReportDiagnostic(diagnostic);
			}

		// check fields and properties
		foreach (var member in recordDeclaration.Members)
		{
			var (unwrappedtype, memberstr, _) = NodeHelpers.GetPropertyOrFieldUnderlyingType(context, member);
			if (unwrappedtype == null) continue;

			// if the property has value semantics, then we're ok
			var (result, errormember) = NodeHelpers.ValueEqualityWrapper(unwrappedtype);
			if (result == ValueEqualityResult.Ok) continue;

			// otherwise, we have a problem. show a diagnostic
			var typestr = unwrappedtype?.ToDisplayString(NullableFlowState.None) ?? "";
			memberstr ??= "?";
			var args = errormember == null ? $"{typestr} {memberstr}" : $"{typestr} {memberstr} ({errormember})";

			var diagnostic = Diagnostic.Create(ParamValueSemanticsRule, member.GetLocation(), args);
			context.ReportDiagnostic(diagnostic);
		}
	}
}
