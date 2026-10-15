namespace RecordValueAnalyser.Test.Legacy;

using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// The .NET 8 SDK and Visual Studio 2022 17.8 ship Roslyn 4.8. A compiler refuses to load an analyzer built against a newer
// Roslyn (CS9057), so the legacy analyzer and code fix must reference exactly 4.8.0.0.

[TestClass]
public class LegacyRoslynTests
{
	private static readonly Version Roslyn48 = new(4, 8, 0, 0);

	private static Version ReferencedVersion(Assembly assembly, string name) =>
		assembly.GetReferencedAssemblies().Single(a => a.Name == name).Version;

	[TestMethod]
	public void AnalyzerReferencesRoslyn48()
	{
		var analyzer = typeof(RecordValueAnalyser).Assembly;

		Assert.AreEqual(Roslyn48, ReferencedVersion(analyzer, "Microsoft.CodeAnalysis"));
		Assert.AreEqual(Roslyn48, ReferencedVersion(analyzer, "Microsoft.CodeAnalysis.CSharp"));
	}

	[TestMethod]
	public void CodeFixReferencesRoslyn48()
	{
		var codeFix = typeof(RecordValueAnalyserCodeFixProvider).Assembly;

		Assert.AreEqual(Roslyn48, ReferencedVersion(codeFix, "Microsoft.CodeAnalysis"));
		Assert.AreEqual(Roslyn48, ReferencedVersion(codeFix, "Microsoft.CodeAnalysis.Workspaces"));
	}
}
