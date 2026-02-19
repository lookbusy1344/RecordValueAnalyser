namespace RecordValueAnalyser.Test.Structs;

using System.Threading.Tasks;
using global::RecordValueAnalyser.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = CSharpCodeFixVerifier<RecordValueAnalyser, RecordValueAnalyserCodeFixProvider>;

// if tests weirdly fail, try deleting files in:
// C:\Users\USERNAME\AppData\Local\Temp\test-packages

// tests for record structs

[TestClass]
public class RecordValueAnalyserUnitTest
{
	private const string coGeneral = TestConstants.General;
	private const string coInlineArrayAttribute = TestConstants.InlineArrayAttribute;

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
	public async Task NestedStructBEqualsPass()
	{
		// the Equals(StructB) member makes this pass
		const string test = coGeneral
							+ """
							  public struct StructB { public int I; public int[] Numbers;
							  	public bool Equals(StructB other) => false;
							  }

							  public record struct A(int I, string S, DateTime Dt, StructB Sb);
							  """;

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task NestedStructBInvalidEqualsFail()
	{
		// the Equals member is invalid because it doesn't take a StructB
		const string test = coGeneral
							+ """
							  public struct StructB { public int I; public int[] Numbers;
							  	public bool Equals(int junk) => false;
							  }

							  public record struct A(int I, string S, DateTime Dt, StructB Sb);
							  """;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(10, 54, 10, 64)
			.WithArguments("StructB Sb (field int[])");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task ObjectMemberFail()
	{
		// object lacks value semantics and should always be flagged
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
	public async Task NestedStructBObjEqualsPass()
	{
		// Equals(object) on a struct is also ok
		const string test = coGeneral
							+ """
							  public struct StructB { public int I; public int[] Numbers;
							  	public override bool Equals(object other) => false;
							  }

							  public record struct A(int I, string S, DateTime Dt, StructB Sb);
							  """;

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task NestedClassWithEqualsObjectPass()
	{
		// an actual class with Equals(object) overridden has value semantics
		const string test = coGeneral
							+ """
							  public class ClassB { public int I; public int[] Numbers;
							  	public override bool Equals(object other) => true;
							  }

							  public record struct A(int I, ClassB Cb);
							  """;

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task NestedClassWithoutEqualsFail()
	{
		// an actual class without Equals lacks value semantics
		const string test = coGeneral
							+ """
							  public class ClassB { public int I; }

							  public record struct A(int I, ClassB Cb);
							  """;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(8, 31, 8, 40)
			.WithArguments("ClassB Cb");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
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

	[TestMethod]
	public async Task ArraySegmentFail()
	{
		// ArraySegment<T> implements IEquatable<T> but compares array identity, not contents
		const string test = coGeneral + "public record struct A(int I, ArraySegment<int> Data);";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 31, 6, 53)
			.WithArguments("System.ArraySegment<int> Data");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task ReadonlyStructWithReferenceFieldFail()
	{
		// A plain readonly struct (not a record struct) containing a reference type should fail.
		// IsRecordType() must not match it.
		const string test = coGeneral
							+ """
							  public readonly struct Wrapper { public readonly List<int> Items; }

							  public record struct A(int I, Wrapper W);
							  """;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(8, 31, 8, 40)
			.WithArguments("Wrapper W (field System.Collections.Generic.List<int>)");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task DecimalMemberPass()
	{
		// decimal is a primitive with value semantics and should produce no diagnostic
		const string test = coGeneral + "public record struct A(decimal D, int I, string S);";

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task BodyPropertyFail()
	{
		// a record body property with an array type should be flagged
		const string test = coGeneral + "public record struct A { public int[] Numbers { get; set; } }";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 26, 6, 60)
			.WithArguments("int[] Numbers");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task BodyFieldFail()
	{
		// a record body field with an array type should be flagged
		const string test = coGeneral + "public record struct A { public string[] Names; }";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 26, 6, 48)
			.WithArguments("string[] Names");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task BodyPropertyPass()
	{
		// a record body property with a value type should not be flagged
		const string test = coGeneral + "public record struct A { public int Count { get; set; } }";

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task MultipleDiagnostics()
	{
		// two failing parameters should both be reported
		const string test = coGeneral + "public record struct A(int Valid, int[] Invalid1, string[] Invalid2);";

		var expected1 = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 35, 6, 49)
			.WithArguments("int[] Invalid1");

		var expected2 = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 51, 6, 68)
			.WithArguments("string[] Invalid2");

		await VerifyCS.VerifyAnalyzerAsync(test, expected1, expected2);
	}

	[TestMethod]
	public async Task InterfaceMemberFail()
	{
		// an interface-typed parameter lacks value semantics and should be flagged
		const string test = coGeneral + "public record struct A(int I, IComparable<int> Comp);";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 31, 6, 52)
			.WithArguments("System.IComparable<int> Comp");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task CodeFixRecordStructSemicolon()
	{
		// code fix on a semicolon-terminated record struct adds braces and readonly stub methods
		const string source = coGeneral + "public record struct A(int[] Data);";
		const string fixedSource = coGeneral
								   + "public record struct A(int[] Data)\n"
								   + "{\n"
								   + "    public readonly bool Equals(A other) => false; // TODO\n"
								   + "    public override readonly int GetHashCode() => 0; // TODO\n"
								   + "}";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 24, 6, 34)
			.WithArguments("int[] Data");

		await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
	}

	[TestMethod]
	public async Task CodeFixRecordStructBraced()
	{
		// code fix on a record struct that already has braces inserts readonly stub methods inside them
		const string source = coGeneral
							  + "\npublic record struct A(int[] Data)\n"
							  + "{\n"
							  + "}\n";
		const string fixedSource = coGeneral
								   + "\npublic record struct A(int[] Data)\n"
								   + "{\n"
								   + "    public readonly bool Equals(A other) => false; // TODO\n"
								   + "    public override readonly int GetHashCode() => 0; // TODO\n"
								   + "}\n";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(7, 24, 7, 34)
			.WithArguments("int[] Data");

		await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
	}
}
