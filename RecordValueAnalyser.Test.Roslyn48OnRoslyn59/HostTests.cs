namespace RecordValueAnalyser.Test.Host;

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// The other tests in this project are only meaningful when the analyzer under test is the Roslyn 4.8 build and the
// compiler running it is Roslyn 5.9.

[TestClass]
public class HostTests
{
	private static readonly Version Roslyn48 = new(4, 8, 0, 0);
	private static readonly Version Roslyn59 = new(5, 9, 0, 0);

	[TestMethod]
	public void AnalyzerIsTheRoslyn48Build()
	{
		var reference = typeof(RecordValueAnalyser).Assembly.GetReferencedAssemblies().Single(a => a.Name == "Microsoft.CodeAnalysis");

		Assert.AreEqual(Roslyn48, reference.Version);
	}

	[TestMethod]
	public void HostCompilerIsRoslyn59()
	{
		Assert.AreEqual(Roslyn59, typeof(Compilation).Assembly.GetName().Version);
	}
}
