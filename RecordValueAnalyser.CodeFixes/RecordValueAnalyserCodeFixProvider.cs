using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// #pragma warning disable IDE0079 // Remove unnecessary suppression
// #pragma warning disable CS0618 // Type or member is obsolete
// #pragma warning disable CS0162 // Unreachable code detected

namespace RecordValueAnalyser
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RecordValueAnalyserCodeFixProvider)), Shared]
	public class RecordValueAnalyserCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RecordValueAnalyser.DiagnosticId);

		public sealed override FixAllProvider GetFixAllProvider() =>
			// See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
			WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			var diagnostic = context.Diagnostics[0];
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			// Find the record class declaration identified by the diagnostic.
			var recdeclaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<RecordDeclarationSyntax>().FirstOrDefault();
			if (recdeclaration == null) return;

			var isrecordclass = (recdeclaration.Kind() == SyntaxKind.RecordDeclaration);
			var isrecordstruct = (recdeclaration.Kind() == SyntaxKind.RecordStructDeclaration);

			if (isrecordclass)
			{
				// Register a code action for record class
				context.RegisterCodeFix(
					CodeAction.Create(
						title: CodeFixResources.CodeFixTitle,
						createChangedSolution: c => FixRecordClassAsync(context.Document, recdeclaration, c),
						equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
					diagnostic);
			}
			else if (isrecordstruct)
			{
				// Register a code action for record struct
				context.RegisterCodeFix(
					CodeAction.Create(
						title: CodeFixResources.CodeFixTitle,
						createChangedSolution: c => FixRecordStructAsync(context.Document, recdeclaration, c),
						equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
					diagnostic);
			}
		}

#pragma warning disable IDE0060 // Remove unused parameter
		private async Task<Solution> FixRecordClassAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
		{
			await Task.CompletedTask.ConfigureAwait(false);
			return document.Project.Solution;

			/*
			// Compute new uppercase name.
			var identifierToken = typeDecl.Identifier;
			var newName = identifierToken.Text.ToUpperInvariant();

			// Get the symbol representing the type to be renamed.
			var semanticModel = await document.GetSemanticModelAsync(cancellationToken)
				.ConfigureAwait(false);
			var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

			// Produce a new solution that has all references to that type renamed, including the declaration.
			//var originalSolution = document.Project.Solution;
			//var optionSet = originalSolution.Workspace.Options; // this is obsolete

			var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol,
				  new SymbolRenameOptions(), newName, cancellationToken)
				.ConfigureAwait(false);

			// Return the new solution with the now-uppercase type name.
			return newSolution;
			*/
		}

		private async Task<Solution> FixRecordStructAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
		{
			await Task.CompletedTask.ConfigureAwait(false);
			return document.Project.Solution;
		}
	}
}
