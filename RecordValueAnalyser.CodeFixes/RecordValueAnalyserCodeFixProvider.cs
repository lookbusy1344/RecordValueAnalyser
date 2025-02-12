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

namespace RecordValueAnalyser;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RecordValueAnalyserCodeFixProvider)), Shared]
public class RecordValueAnalyserCodeFixProvider : CodeFixProvider
{
	private const string ToDoString = " // TODO";

	public sealed override ImmutableArray<string> FixableDiagnosticIds => [RecordValueAnalyser.DiagnosticId];

	public sealed override FixAllProvider GetFixAllProvider() =>
		// See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
		WellKnownFixAllProviders.BatchFixer;

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

		// get the location of the diagnostic
		var diagnostic = context.Diagnostics[0];
		var diagnosticSpan = diagnostic.Location.SourceSpan;

		// Find the type declaration identified by the diagnostic.
		var declaration = root!.FindToken(diagnosticSpan.Start).Parent!.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

		// a record class or struct?
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
		/*	public virtual bool Equals(Self? other) => false;
			public override int GetHashCode() => 0;

			..or for record structs..

			public readonly bool Equals(Self other) => false;
			public override readonly int GetHashCode() => 0;
		 */

		// get the type we're working on
		var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

		// name of the record, for use in Equals(T)
		var recordname = typeDecl.Identifier.Text;

		// build new Equals and GetHashCode methods
		var equalsmethod = isclassrecord ? BuildEqualsClassMethod(recordname) : BuildEqualsStructMethod(recordname);
		var gethashcodemethod = isclassrecord ? BuildClassGetHashCode() : BuildStructGetHashCode();

		// find the record declaration in the syntax tree
		var recordDeclaration = await typeSymbol!.DeclaringSyntaxReferences[0]
			.GetSyntaxAsync()
			.ConfigureAwait(false) as RecordDeclarationSyntax;

		// check if the recordDeclaration has OpenBraceToken '{'
		var hasbraces = recordDeclaration!.OpenBraceToken.IsKind(SyntaxKind.OpenBraceToken);

		RecordDeclarationSyntax updatedDeclaration;
		if (hasbraces) {
			// We already have braces '{ }', so just add the members
			updatedDeclaration = recordDeclaration.AddMembers(equalsmethod, gethashcodemethod);
		} else {
			// no braces, so we need to add them
			// remove any semi-colon, and add the members inside braces '{ }'
			updatedDeclaration = recordDeclaration
				.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
				.WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
				.WithMembers([equalsmethod, gethashcodemethod])
				.WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));
		}

		// replace the record in the syntax tree
		var oldRoot = await document.GetSyntaxRootAsync().ConfigureAwait(false);
		var newRoot = oldRoot!.ReplaceNode(recordDeclaration, updatedDeclaration);

		// To get a new document with the updated syntax tree
		var newDocument = document.WithSyntaxRoot(newRoot);

		return newDocument.Project.Solution;
	}

	/// <summary>
	/// Helper to build: public override readonly int GetHashCode() => 0;
	/// </summary>
	private MethodDeclarationSyntax BuildStructGetHashCode() => SyntaxFactory.MethodDeclaration(
		SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)), "GetHashCode")
		.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword))
		.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0))))
		.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
		.WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Comment(ToDoString), SyntaxFactory.CarriageReturnLineFeed));

	/// <summary>
	/// Helper to build: public override int GetHashCode() => 0;
	/// </summary>
	private MethodDeclarationSyntax BuildClassGetHashCode() => SyntaxFactory.MethodDeclaration(
		SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)), "GetHashCode")
		.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
		.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0))))
		.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
		.WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Comment(ToDoString), SyntaxFactory.CarriageReturnLineFeed));

	/// <summary>
	/// Helper to build: public readonly bool Equals(Self other) => true;
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
		.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
		.WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Comment(ToDoString), SyntaxFactory.CarriageReturnLineFeed));

	/// <summary>
	/// Helper to build: public virtual bool Equals(Self? other) => true;
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
		.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
		.WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Comment(ToDoString), SyntaxFactory.CarriageReturnLineFeed));
}
