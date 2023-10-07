﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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
		public async Task ValueTypesOnly()
		{
			// all good!
			const string test = coGeneral + "public record class A(int I, string S, DateTime Dt);";

			await VerifyCS.VerifyAnalyzerAsync(test);
		}

		[TestMethod]
		public async Task ReadOnlyList()
		{
			// this fails because IReadOnlyList lacks value semantics
			const string test = coGeneral + "public record class A(int I, string S, IReadOnlyList<int> Fail);";

			var expected = VerifyCS.Diagnostic("JSV01")
				.WithSpan(6, 40, 6, 63)
				.WithArguments("System.Collections.Generic.IReadOnlyList<int> Fail");

			await VerifyCS.VerifyAnalyzerAsync(test, expected);
		}

		[TestMethod]
		public async Task NestedStructPass()
		{
			// the nested struct is ok, because all the members are value types
			const string test = coGeneral
				+ """
				public struct StructA { public int I; public string S; }

				public record class A(int I, string S, DateTime Dt, StructA Sa);
				""";

			await VerifyCS.VerifyAnalyzerAsync(test);
		}

		[TestMethod]
		public async Task NestedStructFail()
		{
			// the array nested in the struct makes this fail
			const string test = coGeneral
				+ """
				public struct StructA { public int I; public int[] Numbers; }

				public record class A(int I, string S, DateTime Dt, StructA Sa);
				""";

			var expected = VerifyCS.Diagnostic("JSV01")
				.WithSpan(8, 53, 8, 63)
				.WithArguments("StructA Sa (int[])");

			await VerifyCS.VerifyAnalyzerAsync(test, expected);
		}

		[TestMethod]
		public async Task NestedStructEqualsPass()
		{
			// the Equals member makes this pass
			const string test = coGeneral
				+ """
				public struct StructA { public int I; public int[] Numbers; 
					public bool Equals(StructA other) => false; 
				}

				public record class A(int I, string S, DateTime Dt, StructA Sa);
				""";

			await VerifyCS.VerifyAnalyzerAsync(test);
		}

		[TestMethod]
		public async Task NestedClassEqualsPass()
		{
			// the Equals member makes this pass
			const string test = coGeneral
				+ """
				public struct ClassA { public int I; public int[] Numbers; 
					public bool Equals(ClassA other) => false; 
				}

				public record class A(int I, string S, DateTime Dt, ClassA Ca);
				""";

			await VerifyCS.VerifyAnalyzerAsync(test);
		}

		[TestMethod]
		public async Task NestedClassInvalidEqualsFail()
		{
			// the Equals member is invalid because it doesn't take a ClassA
			const string test = coGeneral
				+ """
				public struct ClassA { public int I; public int[] Numbers; 
					public bool Equals(int junk) => false; 
				}

				public record class A(int I, string S, DateTime Dt, ClassA Ca);
				""";

			var expected = VerifyCS.Diagnostic("JSV01")
				.WithSpan(10, 53, 10, 62)
				.WithArguments("ClassA Ca (int[])");

			await VerifyCS.VerifyAnalyzerAsync(test, expected);
		}

		[TestMethod]
		public async Task ObjectMemberFail()
		{
			// the Equals member is invalid because it doesn't take a ClassA
			const string test = coGeneral
				+ """
				public record class A(object O, string S, DateTime Dt);
				""";

			var expected = VerifyCS.Diagnostic("JSV01")
				.WithSpan(6, 23, 6, 31)
				.WithArguments("object O");

			await VerifyCS.VerifyAnalyzerAsync(test, expected);
		}
	}
}
