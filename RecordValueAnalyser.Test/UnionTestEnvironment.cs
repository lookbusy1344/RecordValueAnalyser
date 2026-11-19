namespace RecordValueAnalyser.Test.Unions;

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

/// <summary>
///     Union tests on the compiler shipped with the .NET 11 SDK: C# 15 against the .NET 11 reference assemblies, which
///     hold UnionAttribute and IUnion.
/// </summary>
internal static class UnionTestEnvironment
{
	private const string TargetFramework = "net11.0";
	private const string RefPackageName = "Microsoft.NETCore.App.Ref";
	private const string RefPackageVersion = "11.0.0";
	private const string RefFolder = "ref";

	internal static LanguageVersion LanguageVersion => LanguageVersion.CSharp15;

	internal static ReferenceAssemblies ReferenceAssemblies { get; } = new(
		TargetFramework,
		new(RefPackageName, RefPackageVersion),
		Path.Combine(RefFolder, TargetFramework));

	internal static IReadOnlyList<string> SupportSources { get; } = [];
}
