using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = RecordValueAnalyser.Test.CSharpCodeFixVerifier<RecordValueAnalyser.RecordValueAnalyser, RecordValueAnalyser.RecordValueAnalyserCodeFixProvider>;

// if tests weirdly fail, try deleting files in:
// C:\Users\ps\AppData\Local\Temp\test-packages

#pragma warning disable RCS1046 // Asynchronous method name should end with 'Async'.

namespace RecordValueAnalyser.TestStructs;

// tests for record structs

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
		const string test = coGeneral + "public readonly record struct A(int I, string S, DateTime Dt);";

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task ReadOnlyList()
	{
		// this fails because IReadOnlyList lacks value semantics
		const string test = coGeneral + "public record struct A(int I, string S, IReadOnlyList<int> Fail);";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 41, 6, 64)
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

				public readonly record struct A(int I, string S, DateTime Dt, StructA Sa);
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

				public record struct A(int I, string S, DateTime Dt, StructA Sa);
				""";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(8, 54, 8, 64)
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

				public readonly record struct A(int I, string S, DateTime Dt, StructA Sa);
				""";

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task NestedClassEqualsPass()
	{
		// the Equals(ClassA) member makes this pass
		const string test = coGeneral
			+ """
				public struct ClassA { public int I; public int[] Numbers; 
					public bool Equals(ClassA other) => false; 
				}

				public record struct A(int I, string S, DateTime Dt, ClassA Ca);
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

				public record struct A(int I, string S, DateTime Dt, ClassA Ca);
				""";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(10, 54, 10, 63)
			.WithArguments("ClassA Ca (int[])");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task ObjectMemberFail()
	{
		// the Equals member is invalid because it doesn't take a ClassA
		const string test = coGeneral
			+ """
				public record struct A(object O, string S, DateTime Dt);
				""";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 24, 6, 32)
			.WithArguments("object O");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task NestedClassObjEqualsPass()
	{
		// the Equals(object) is also ok
		const string test = coGeneral
			+ """
				public struct ClassA { public int I; public int[] Numbers; 
					public override bool Equals(object other) => false; 
				}

				public record struct A(int I, string S, DateTime Dt, ClassA Ca);
				""";

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task RecordEqualsMethodPass()
	{
		// If the record has an Equals method, it's ok
		const string test = coGeneral
			+ """
				public readonly record struct A(int I, string S, IReadOnlyList<int> Fail)
				{
					public bool Equals(A other) => false;
				}
				""";

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task RecordWithTuplePass()
	{
		// Tuple are find if they contain value types
		const string test = coGeneral
			+ """
				public record struct Tup1(int IPass, (int a, int b) TupPass, DateTime? DtPass);
				public record struct Tup2(int IPass, (bool, int) TupPass);
				""";

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task RecordWithTupleFail()
	{
		// A tuple containing an array is not ok
		const string test = coGeneral + "public record struct Tup(int IPass, (int a, int[] b) TupFail);";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 37, 6, 61)
			.WithArguments("(int a, int[] b) TupFail (int[])");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}
}
