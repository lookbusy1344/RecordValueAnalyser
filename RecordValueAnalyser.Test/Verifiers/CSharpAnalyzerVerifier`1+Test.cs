﻿using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace RecordValueAnalyser.Test
{
	public static partial class CSharpAnalyzerVerifier<TAnalyzer>
		where TAnalyzer : DiagnosticAnalyzer, new()
	{
		public class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
		{
			public Test()
			{
				SolutionTransforms.Add((solution, projectId) =>
				{
					var compilationOptions = solution.GetProject(projectId).CompilationOptions;
					compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
						compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));
					return solution.WithProjectCompilationOptions(projectId, compilationOptions);
				});
			}
		}
	}
}
