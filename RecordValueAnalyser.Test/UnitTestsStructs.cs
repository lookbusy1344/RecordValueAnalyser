namespace RecordValueAnalyser.Test.Structs;

using System.Threading;
using System.Threading.Tasks;
using global::RecordValueAnalyser.Test;
using Microsoft.CodeAnalysis.Testing;
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
	public async Task NestedStructStaticEqualsFail()
	{
		// static Equals(T) is excluded by HasEqualsTMethod (!m.IsStatic) — struct should still fail
		const string test = coGeneral
							+ """
							  public struct StructA {
							  	public int[] Items;
							  	public static bool Equals(StructA other) => false;
							  }

							  public record struct A(StructA Sa);
							  """;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(11, 24, 11, 34)
			.WithArguments("StructA Sa (field int[])");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task NestedStructNonOverrideEqualsObjectFail()
	{
		// Equals(object) without 'override' is excluded by HasEqualsObjectMethod (m.IsOverride)
		const string test = coGeneral
							+ """
							  public struct StructA {
							  	public int[] Items;
							  	public bool Equals(object other) => false;
							  }

							  public record struct A(StructA Sa);
							  """;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(11, 24, 11, 34)
			.WithArguments("StructA Sa (field int[])");

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
	public async Task PartialRecordEqualsInOtherPartial()
	{
		// Equals(T) defined in a different partial declaration must suppress JSV01
		const string file1 = coGeneral + "public partial record struct A(int[] Data);";
		const string file2 = "public partial record struct A\n{\n    public readonly bool Equals(A other) => false;\n}";

		var test = new VerifyCS.Test();
		test.TestState.Sources.Add(("File1.cs", file1));
		test.TestState.Sources.Add(("File2.cs", file2));
		await test.RunAsync();
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
	public async Task RecordWithNestedTupleFail()
	{
		// recursive descent into nested tuple — outer loop reports the inner tuple as the failing member
		const string test = coGeneral + "public record struct A((int, (string, int[])) T);";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 24, 6, 48)
			.WithArguments("(int, (string, int[])) T (field (string, int[]))");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task RecordWithNestedTuplePass()
	{
		// nested tuple with all value-semantic types — should pass
		const string test = coGeneral + "public record struct A((int, (string, DateTime)) T);";

		await VerifyCS.VerifyAnalyzerAsync(test);
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
	public async Task NestedStructAutoPropertyFailSingleDiagnostic()
	{
		// A struct with an auto-property containing a failing type should produce exactly
		// one diagnostic, not two (one for the property and one for the backing field)
		const string test = coGeneral
							+ """
							  public struct StructWithProp { public int[] Items { get; set; } }

							  public record struct Tester(int I, StructWithProp Sp);
							  """;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(8, 36, 8, 53)
			.WithArguments("StructWithProp Sp (field int[])");

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
	public async Task MemoryFail()
	{
		// Memory<T> compares underlying span identity, not element contents
		const string test = coGeneral + "public record struct A(int I, Memory<int> Data);";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 31, 6, 47)
			.WithArguments("System.Memory<int> Data");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task ReadOnlyMemoryFail()
	{
		// ReadOnlyMemory<T> compares underlying span identity, not element contents
		const string test = coGeneral + "public record struct A(int I, ReadOnlyMemory<int> Data);";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 31, 6, 55)
			.WithArguments("System.ReadOnlyMemory<int> Data");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task RecordWithImmutableArray()
	{
		// ImmutableArray<T> lacks value semantics — struct parity with class test
		const string test = coGeneral
							+ """
							  public record struct Tester(int I, System.Collections.Immutable.ImmutableArray<int> Numbers);
							  """;

		var expected = VerifyCS
			.Diagnostic()
			.WithSpan(6, 36, 6, 92)
			.WithArguments("System.Collections.Immutable.ImmutableArray<int> Numbers");

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
	public async Task NativeIntegerPass()
	{
		// nint/nuint are not in IsPrimitiveType but IntPtr/UIntPtr implement IEquatable<T> — should pass
		const string test = coGeneral + "public record struct A(nint I, nuint U);";

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task FlagsEnumPass()
	{
		// [Flags] enum is still TypeKind.Enum — HasSimpleEquality returns true
		const string test = coGeneral
							+ """
							  [Flags] public enum MyFlags { A = 1, B = 2, C = 4 }

							  public record struct A(MyFlags F);
							  """;

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task EnumWithByteUnderlyingTypePass()
	{
		// enum with non-default underlying type is still TypeKind.Enum — HasSimpleEquality returns true
		const string test = coGeneral
							+ """
							  public enum ByteEnum : byte { X = 0, Y = 1, Z = 255 }

							  public record struct A(ByteEnum E);
							  """;

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task NullableStringMemberPass()
	{
		// nullable reference types with value semantics should not produce a diagnostic
		const string test = "#nullable enable\n" + coGeneral + "public record struct A(string? Name, int I);";

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
	public async Task MultiVariableFieldFail()
	{
		// each variable in a multi-variable field declaration should produce its own diagnostic
		const string test = coGeneral + "public record struct A { public int[] X, Y, Z; }";

		var expectedX = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 26, 6, 47)
			.WithArguments("int[] X");

		var expectedY = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 26, 6, 47)
			.WithArguments("int[] Y");

		var expectedZ = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 26, 6, 47)
			.WithArguments("int[] Z");

		await VerifyCS.VerifyAnalyzerAsync(test, expectedX, expectedY, expectedZ);
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

	[TestMethod]
	public async Task GenericRecordUnconstrainedFail()
	{
		// unconstrained T has no guaranteed equality — should be flagged
		const string test = coGeneral + "public record struct Wrapper<T>(T Value);";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 33, 6, 40)
			.WithArguments("T Value");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericRecordStructConstrainedFail()
	{
		// T : struct still has no guaranteed Equals(T) — should be flagged (current behaviour)
		const string test = coGeneral + "public record struct Wrapper<T>(T Value) where T : struct;";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 33, 6, 40)
			.WithArguments("T Value");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericRecordIEquatableConstrainedFail()
	{
		// T : IEquatable<T> still falls through to Failed — type parameter GetMembers() does not expose interface members
		const string test = coGeneral + "public record struct Wrapper<T>(T Value) where T : IEquatable<T>;";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 33, 6, 40)
			.WithArguments("T Value");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericRecordClassConstrainedFail()
	{
		// T : class still falls through to Failed — class-constrained T has no guaranteed Equals(T)
		const string test = coGeneral + "public record struct Wrapper<T>(T Value) where T : class;";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 33, 6, 40)
			.WithArguments("T Value");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericRecordNotNullConstrainedFail()
	{
		// T : notnull still falls through to Failed — notnull does not imply value semantics
		const string test = coGeneral + "public record struct Wrapper<T>(T Value) where T : notnull;";

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 33, 6, 40)
			.WithArguments("T Value");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task MixedBodyAndParameterFail()
	{
		// both a failing parameter and a failing body field should each produce a diagnostic
		const string test = coGeneral + "public record struct A(int[] BadParam) { public string[] BadBody; }";

		var expected1 = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 24, 6, 38)
			.WithArguments("int[] BadParam");

		var expected2 = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 42, 6, 66)
			.WithArguments("string[] BadBody");

		await VerifyCS.VerifyAnalyzerAsync(test, expected1, expected2);
	}

	[TestMethod]
	public async Task TriplyNestedStructFail()
	{
		// three levels of struct nesting — the innermost bad member should still be caught
		const string test = coGeneral
							+ """
							  public struct StructC { public int[] Items { get; set; } }
							  public struct StructB { public StructC C { get; set; } }
							  public struct StructA { public StructB B { get; set; } }

							  public record struct Tester(int I, StructA Sa);
							  """;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(10, 36, 10, 46)
			.WithArguments("StructA Sa (field StructB)");

		await VerifyCS.VerifyAnalyzerAsync(test, expected);
	}

	[TestMethod]
	public async Task DuplicateNestedTypeAcrossSiblingFieldsPass()
	{
		// A struct type that appears at two sibling positions should be checked at both.
		// Previously the accumulating visited set would skip the second occurrence.
		const string test = coGeneral
							+ """
							  public struct Good { public int X; }
							  public struct Container { public Good A; public Good B; }

							  public record struct R(Container C);
							  """;

		await VerifyCS.VerifyAnalyzerAsync(test);
	}

	[TestMethod]
	public async Task CircularStructDirectCycleNoCrash()
	{
		// A → B → A circular struct layout (CS0523). Analyzer must not crash and must not report JSV01.
		const string test = coGeneral
							+ """
							  struct A { public B b; }
							  struct B { public A a; }
							  public record struct R(A field);
							  """;

		var t = new VerifyCS.Test { TestCode = test, CompilerDiagnostics = CompilerDiagnostics.None };
		await t.RunAsync(CancellationToken.None);
	}

	[TestMethod]
	public async Task CircularStructIndirectCycleNoCrash()
	{
		// X → Y → Z → X three-type circular struct layout (CS0523). Analyzer must not crash.
		const string test = coGeneral
							+ """
							  struct X { public Y y; }
							  struct Y { public Z z; }
							  struct Z { public X x; }
							  public record struct R(X field);
							  """;

		var t = new VerifyCS.Test { TestCode = test, CompilerDiagnostics = CompilerDiagnostics.None };
		await t.RunAsync(CancellationToken.None);
	}

	[TestMethod]
	public async Task CircularStructWithInvalidMemberFails()
	{
		// A contains B (cycle) but also int[] Items which lacks value semantics.
		// The analyzer must report JSV01 for int[] and not crash.
		const string test = coGeneral
							+ """
							  struct A { public B b; public int[] Items; }
							  struct B { public A a; }
							  public record struct R(A field);
							  """;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(8, 24, 8, 31)
			.WithArguments("A field (field int[])");

		var t = new VerifyCS.Test { TestCode = test, CompilerDiagnostics = CompilerDiagnostics.None };
		t.ExpectedDiagnostics.Add(expected);
		await t.RunAsync(CancellationToken.None);
	}
}
