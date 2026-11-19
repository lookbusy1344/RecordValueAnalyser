namespace RecordValueAnalyser.Test.Unions;

using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

/// <summary>
///     Union tests on Roslyn 5.9, the oldest compiler with union types. It has no LanguageVersion.CSharp15, so unions need
///     Preview, and the default reference assemblies lack the union runtime types, so the tests declare them.
/// </summary>
internal static class UnionTestEnvironment
{
	internal static LanguageVersion LanguageVersion => LanguageVersion.Preview;

	internal static ReferenceAssemblies ReferenceAssemblies => ReferenceAssemblies.Default;

	internal static IReadOnlyList<string> SupportSources { get; } = [TestConstants.UnionTypes];
}
