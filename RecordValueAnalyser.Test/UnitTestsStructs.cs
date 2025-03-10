﻿namespace RecordValueAnalyser.Test.Structs;

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = CSharpCodeFixVerifier<RecordValueAnalyser, RecordValueAnalyserCodeFixProvider>;

// if tests weirdly fail, try deleting files in:
// C:\Users\USERNAME\AppData\Local\Temp\test-packages

// tests for record structs

[TestClass]
public class RecordValueAnalyserUnitTest
{
	private const string coGeneral = @"
using System;
using System.Collections.Generic;

namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
";

	// This is needed for testing inline arrays, which are .NET 8 only
	// Its a stub for the real attribute
	// https://github.com/dotnet/runtime/issues/61135
	// https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.inlinearrayattribute?view=net-8.0
	private const string coInlineArrayAttribute = @"
namespace System.Runtime.CompilerServices {
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class InlineArrayAttribute : Attribute {
        public InlineArrayAttribute (int length) { Length = length; }
        public int Length { get; }
    } }
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
			.WithArguments("StructA Sa (field int[])");

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
			.WithArguments("ClassA Ca (field int[])");

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
			.WithArguments("(int a, int[] b) TupFail (field int[])");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task RecordWithDelegate()
	{
		// A record containing a delegate not ok
		const string test = coGeneral
			+ """
			public delegate int ExampleDelegate(string str1, string str2);

			public record struct Tester(ExampleDelegate Ex, int? I, string St);
			""";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(8, 29, 8, 47)
			.WithArguments("ExampleDelegate Ex");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task RecordWithDynamic()
	{
		// A record containing a struct with a dynamic member. Not ok
		const string test = coGeneral
			+ """
			public struct StructDynamic { public dynamic Dy { get; set; } }

			public record struct Tester(int I, StructDynamic Dy);
			""";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(8, 36, 8, 52)
			.WithArguments("StructDynamic Dy (field dynamic)");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task RecordWithDoubleNestedStructPass()
	{
		// A record containing a double-nested struct
		const string test = coGeneral
			+ """
			public struct StructB { public string Str { get; set; } }
			public struct StructA { public double? Number { get; set; } public StructB StB { get; set; } }

			public record struct Tester(int I, StructA Sa);
			""";

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task RecordWithDoubleNestedStructFail()
	{
		// A record containing a double-nested struct, should fail
		const string test = coGeneral
			+ """
			public struct StructB { public double[] Dbl { get; set; } }
			public struct StructA { public int? Number { get; set; } public StructB StB { get; set; } }

			public record struct Tester(int I, StructA Sa);
			""";

		var expected = VerifyCS.Diagnostic()
			.WithSpan(9, 36, 9, 46)
			.WithArguments("StructA Sa (field StructB)");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task RecordWithInlineArray()
	{
		// A record containing a .NET 8 inline array, should fail
		const string test = coGeneral
			+ coInlineArrayAttribute
			+ """
			[System.Runtime.CompilerServices.InlineArray(3)]
			public struct MyInlineArray { public byte _element0; }
			
			public readonly record struct Tester(int I, MyInlineArray Ar);
			""";

		// bodge for "Target runtime doesn't support inline array types."
		var unsupported = Microsoft.CodeAnalysis.Testing.DiagnosticResult
			.CompilerError("CS9171")
			.WithSpan(14, 15, 14, 28);

		// the expected error
		var expected = VerifyCS.Diagnostic()
			.WithSpan(16, 45, 16, 61)
			.WithArguments("MyInlineArray Ar");

		await VerifyCS.VerifyAnalyzerAsync(test, unsupported, expected);
	}

	[TestMethod]
	public async Task MissingInlineArrayAttrib()
	{
		// A record containing a .NET 8 inline array, but lacking the attribute
		// should pass because it is therefore a normal struct
		const string test = coGeneral
			+ """
			public struct MyInlineArray { public byte _element0; }
			
			public readonly record struct Tester(int I, MyInlineArray Ar);
			""";

		await VerifyCS.VerifyAnalyzerAsync(test);
	}
}
