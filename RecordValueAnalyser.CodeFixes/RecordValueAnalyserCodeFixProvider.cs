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

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS0162 // Unreachable code detected

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

			// TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
			var diagnostic = context.Diagnostics[0];
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			// Find the type declaration identified by the diagnostic.
			var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

			var isclass = declaration.Kind() == SyntaxKind.RecordDeclaration;

			// Register a code action that will invoke the fix.
			context.RegisterCodeFix(
				CodeAction.Create(
					title: CodeFixResources.CodeFixTitle,
					createChangedSolution: c => FixEqualsAsync(context.Document, declaration, isclass, c),
					equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
				diagnostic);
		}

		private async Task<Solution> FixEqualsAsync(Document document, TypeDeclarationSyntax typeDecl, bool isclassrecord, CancellationToken cancellationToken)
		{
			/*	public virtual bool Equals(Self? other) => throw new NotImplementedException();
				..or for record structs..
				public readonly bool Equals(Self other) => throw new NotImplementedException();

				public override int GetHashCode() => 0;
			 */

			// get the type we're working on
			var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
			var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

			var recordname = typeDecl.Identifier.Text;

			// build new Equals and GetHashCode methods
			var equalsmethod = isclassrecord ? BuildEqualsClassMethod(recordname) : BuildEqualsStructMethod(recordname);
			var gethashcodemethod = BuildGetHashCode(!isclassrecord);

			// find the record declaration in the syntax tree
			var recordDeclaration = (RecordDeclarationSyntax)await typeSymbol
				.DeclaringSyntaxReferences[0]
				.GetSyntaxAsync()
				.ConfigureAwait(false);

			// check if record already has members
			//var hasmembers = recordDeclaration.Members.Any();

			// add the new methods to the record
			var methods = SyntaxFactory.List<MemberDeclarationSyntax>(nodes: new MemberDeclarationSyntax[] { equalsmethod, gethashcodemethod });
			var updatedDeclaration = recordDeclaration.WithMembers(methods);

			// replace the record in the syntax tree
			var oldRoot = await document.GetSyntaxRootAsync().ConfigureAwait(false);
			var newRoot = oldRoot.ReplaceNode(recordDeclaration, updatedDeclaration);

			// To get a new document with the updated syntax tree
			var newDocument = document.WithSyntaxRoot(newRoot);

			return newDocument.Project.Solution;
		}

		/// <summary>
		/// Helper to build: public override [readonly] int GetHashCode() => 0;
		/// </summary>
		private MethodDeclarationSyntax BuildGetHashCode(bool readonlymethod) => SyntaxFactory.MethodDeclaration(
			SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)), "GetHashCode")
			.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword),
				readonlymethod ? SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword) : SyntaxFactory.Token(SyntaxKind.None))
			.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0))))
			.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

		/// <summary>
		/// Helper to build: public readonly bool Equals(Self other) => throw new NotImplmentedException();
		/// </summary>
		private MethodDeclarationSyntax BuildEqualsStructMethod(string recordname) => SyntaxFactory.MethodDeclaration(
				SyntaxFactory.PredefinedType(
					SyntaxFactory.Token(SyntaxKind.BoolKeyword)), "Equals")
			.WithModifiers(
				SyntaxFactory.TokenList(
					SyntaxFactory.Token(SyntaxKind.PublicKeyword),
					SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
			.WithParameterList(
				SyntaxFactory.ParameterList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier("other"))
							.WithType(SyntaxFactory.ParseTypeName(recordname)))))
			.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)))
			.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

		/// <summary>
		/// Helper to build: public virtual bool Equals(Self? other) => throw new NotImplmentedException();
		/// </summary>
		private MethodDeclarationSyntax BuildEqualsClassMethod(string recordname) => SyntaxFactory.MethodDeclaration(
				SyntaxFactory.PredefinedType(
					SyntaxFactory.Token(SyntaxKind.BoolKeyword)), "Equals")
			.WithModifiers(
				SyntaxFactory.TokenList(
					SyntaxFactory.Token(SyntaxKind.PublicKeyword),
					SyntaxFactory.Token(SyntaxKind.VirtualKeyword)))
			.WithParameterList(
				SyntaxFactory.ParameterList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Parameter(
							SyntaxFactory.Identifier("other"))
							.WithType(
								SyntaxFactory.NullableType(
									SyntaxFactory.ParseTypeName(recordname))))))
			.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)))
			.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
	}
}
