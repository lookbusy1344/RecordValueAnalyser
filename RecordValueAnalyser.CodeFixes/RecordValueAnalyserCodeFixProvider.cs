﻿using Microsoft.CodeAnalysis;
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
	public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RecordValueAnalyser.DiagnosticId);

	public sealed override FixAllProvider GetFixAllProvider() =>
		// See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
		WellKnownFixAllProviders.BatchFixer;

	/// <summary>
	/// Register code fix for record classes and record structs
	/// </summary>
	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

		if (root == null || context.Diagnostics.IsEmpty) return;

		var diagnostic = context.Diagnostics[0];
		if (diagnostic == null) return;

		var diagnosticSpan = diagnostic.Location.SourceSpan;

		// Find the record class declaration identified by the diagnostic.
		var recdeclaration = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<RecordDeclarationSyntax>().FirstOrDefault();
		if (recdeclaration == null) return;

		// is it 'record class' or 'record struct'
		var isclass = recdeclaration.Kind() == SyntaxKind.RecordDeclaration;

		// Register a code action for record class
		context.RegisterCodeFix(
			CodeAction.Create(
				title: CodeFixResources.CodeFixTitle,
				createChangedSolution: c => FixRecordAsync(context.Document, recdeclaration, isclass, c),
				equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
			diagnostic);
	}

	/// <summary>
	/// Code fix for record by adding Equals and GetHashCode
	/// </summary>
	private async Task<Solution> FixRecordAsync(Document document, TypeDeclarationSyntax typeDecl, bool isclass, CancellationToken cancellationToken)
	{
		/*	public virtual bool Equals(Self? other) => throw new NotSupported();
		 	..or for record structs..
		 	public readonly bool Equals(Self other) => throw new NotSupported();
		 	
			public override int GetHashCode() => 0;
		 */

		var recordname = typeDecl.Identifier.ValueText;

		var equalsmethod = isclass ? BuildEqualsClassMethod(recordname) : BuildEqualsStructMethod(recordname);
		var gethashcodemethod = BuildGetHashCode();

		var newRoot = await document.GetSyntaxRootAsync(cancellationToken)
			.ConfigureAwait(false);
		var _ = newRoot!.InsertNodesAfter(
			newRoot!.DescendantNodes().OfType<RecordDeclarationSyntax>().First().Members.Last(),
			new[] { equalsmethod, gethashcodemethod });

		var newDocument = document.WithSyntaxRoot(newRoot);

		return newDocument.Project.Solution;
	}

	/// <summary>
	/// Helper to build public override int GetHashCode()
	/// </summary>
	private MethodDeclarationSyntax BuildGetHashCode() => SyntaxFactory.MethodDeclaration(
			SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)), "GetHashCode")
		.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
		.WithBody(
			SyntaxFactory.Block(
				SyntaxFactory.ReturnStatement(
					SyntaxFactory.LiteralExpression(
						SyntaxKind.NumericLiteralExpression,
						SyntaxFactory.Literal(0)))));

	/// <summary>
	/// Helper to build public readonly bool Equals(Self other)
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
						.WithType(
								SyntaxFactory.ParseTypeName(recordname)))))
		.WithBody(
			SyntaxFactory.Block(
				SyntaxFactory.SingletonList<StatementSyntax>(
					SyntaxFactory.ReturnStatement(
						SyntaxFactory.BinaryExpression(
							SyntaxKind.EqualsExpression,
							SyntaxFactory.ThisExpression(),
							SyntaxFactory.IdentifierName("other"))))));

	/// <summary>
	/// Helper to build public virtual bool Equals(Self? other)
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
		.WithBody(
			SyntaxFactory.Block(
				SyntaxFactory.SingletonList<StatementSyntax>(
					SyntaxFactory.ReturnStatement(
						SyntaxFactory.BinaryExpression(
							SyntaxKind.EqualsExpression,
							SyntaxFactory.ThisExpression(),
							SyntaxFactory.IdentifierName("other"))))));
}
