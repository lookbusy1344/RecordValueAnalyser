using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Testing;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1000 // Do not declare static members on generic types

namespace RecordValueAnalyser.Test
{
	public static partial class CSharpCodeRefactoringVerifier<TCodeRefactoring>
		where TCodeRefactoring : CodeRefactoringProvider, new()
	{
		/// <inheritdoc cref="CodeRefactoringVerifier{TCodeRefactoring, TTest, TVerifier}.VerifyRefactoringAsync(string, string)"/>
		public static async Task VerifyRefactoringAsync(string source, string fixedSource) => await VerifyRefactoringAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource);

		/// <inheritdoc cref="CodeRefactoringVerifier{TCodeRefactoring, TTest, TVerifier}.VerifyRefactoringAsync(string, DiagnosticResult, string)"/>
		public static async Task VerifyRefactoringAsync(string source, DiagnosticResult expected, string fixedSource) => await VerifyRefactoringAsync(source, new[] { expected }, fixedSource);

		/// <inheritdoc cref="CodeRefactoringVerifier{TCodeRefactoring, TTest, TVerifier}.VerifyRefactoringAsync(string, DiagnosticResult[], string)"/>
		public static async Task VerifyRefactoringAsync(string source, DiagnosticResult[] expected, string fixedSource)
		{
			var test = new Test
			{
				TestCode = source,
				FixedCode = fixedSource,
			};

			test.ExpectedDiagnostics.AddRange(expected);
			await test.RunAsync(CancellationToken.None);
		}
	}
}
