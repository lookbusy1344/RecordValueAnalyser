using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = RecordValueAnalyser.Test.CSharpCodeFixVerifier<RecordValueAnalyser.RecordValueAnalyser, RecordValueAnalyser.RecordValueAnalyserCodeFixProvider>;

// if tests weirdly fail, try deleting files in:
// C:\Users\ps\AppData\Local\Temp\test-packages

#pragma warning disable RCS1046 // Asynchronous method name should end with 'Async'.

namespace RecordValueAnalyser.Test
{
	[TestClass]
	public class RecordValueAnalyserUnitTest
	{
		private const string coGeneral = @"
using System;
using System.Collections.Generic;

namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
";

		[TestMethod]
		public async Task ValueTypesOnlyTest()
		{
			const string test = coGeneral + "public record class A(int I, string S, DateTime Dt);";

			await VerifyCS.VerifyAnalyzerAsync(test);
		}

		[TestMethod]
		public async Task ReadOnlyListTest()
		{
			const string test = coGeneral + "public record class A(int I, string S, IReadOnlyList<int> Fail);";

			var expected = VerifyCS.Diagnostic("JSV01")
				.WithSpan(6, 40, 6, 63)
				.WithArguments("System.Collections.Generic.IReadOnlyList<int> Fail");

			await VerifyCS.VerifyAnalyzerAsync(test, expected);
		}
	}
}
