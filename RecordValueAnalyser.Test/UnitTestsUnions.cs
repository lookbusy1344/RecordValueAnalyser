namespace RecordValueAnalyser.Test.Unions;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = CSharpCodeFixVerifier<RecordValueAnalyser, RecordValueAnalyserCodeFixProvider>;

// tests for records holding C# 15 union types
// A union struct stores its case in an object field, and ValueType.Equals compares that field with object.Equals,
// so a union has value semantics exactly when every case type has value semantics.

[TestClass]
public class RecordValueAnalyserUnitTest
{
	private const string coGeneral = TestConstants.General;
	private const string coUnionTypes = TestConstants.UnionTypes;

	/// <summary>
	///     Roslyn 5.9.0 has no LanguageVersion.CSharp15, so unions need Preview
	/// </summary>
	private static async Task VerifyPreviewAsync(string source, params DiagnosticResult[] expected)
	{
		var test = new VerifyCS.Test { TestCode = source };
		test.SolutionTransforms.Add((solution, projectId) => {
			var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
			return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
		});
		test.ExpectedDiagnostics.AddRange(expected);
		await test.RunAsync(CancellationToken.None);
	}

	[TestMethod]
	public async Task UnionOfRecordsPass()
	{
		// both case types are records
		const string test = coGeneral
							+ """
							  public record A(Pet P);

							  public record Cat(string Name);
							  public record Dog(int Age);
							  public union Pet(Cat, Dog);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task UnionOfPrimitivesPass()
	{
		// boxed primitives and strings compare by value through object.Equals
		const string test = coGeneral
							+ """
							  public record A(Num N);

							  public union Num(int, string, decimal);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task UnionInRecordStructPass()
	{
		const string test = coGeneral
							+ """
							  public record struct A(Pet P);

							  public record Cat(string Name);
							  public record Dog(int Age);
							  public union Pet(Cat, Dog);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task UnionWithClassCaseFail()
	{
		// Cat is a class without Equals, so the union compares Cat by reference
		const string test = coGeneral
							+ """
							  public record A(Pet P);

							  public class Cat { public string Name = ""; }
							  public record Dog(int Age);
							  public union Pet(Cat, Dog);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 22)
			.WithArguments("Pet P (field Cat)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionWithArrayCaseFail()
	{
		// the second case is an array, reported in preference to passing cases before it
		const string test = coGeneral
							+ """
							  public record A(Data D);

							  public union Data(int, int[]);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 23)
			.WithArguments("Data D (field int[])");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionWithInterfaceCaseFail()
	{
		const string test = coGeneral
							+ """
							  public record A(Data D);

							  public union Data(int, IReadOnlyList<int>);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 23)
			.WithArguments("Data D (field System.Collections.Generic.IReadOnlyList<int>)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionWithNestedStructCaseFail()
	{
		// the case struct holds an array, so the case fails and is named
		const string test = coGeneral
							+ """
							  public record A(Data D);

							  public struct StructA { public int I; public int[] Numbers; }
							  public union Data(int, StructA);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 23)
			.WithArguments("Data D (field StructA)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task NullableUnionPass()
	{
		const string test = coGeneral
							+ """
							  public record A(Pet? P);

							  public record Cat(string Name);
							  public record Dog(int Age);
							  public union Pet(Cat, Dog);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task NullableUnionFail()
	{
		const string test = coGeneral
							+ """
							  public record A(Pet? P);

							  public class Cat { public string Name = ""; }
							  public record Dog(int Age);
							  public union Pet(Cat, Dog);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 23)
			.WithArguments("Pet? P (field Cat)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionPropertyFail()
	{
		// unions are checked as record properties, not only as positional parameters
		const string test = coGeneral
							+ """
							  public record A { public Data D { get; init; } }

							  public union Data(int, int[]);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 19, 6, 47)
			.WithArguments("Data D (field int[])");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task NestedUnionPass()
	{
		// a union case can itself be a union
		const string test = coGeneral
							+ """
							  public record A(Outer O);

							  public record Cat(string Name);
							  public union Inner(Cat, int);
							  public union Outer(Inner, string);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task NestedUnionFail()
	{
		// the failure is in a case of the inner union; the outer report names the inner union
		const string test = coGeneral
							+ """
							  public record A(Outer O);

							  public class Cat { public string Name = ""; }
							  public union Inner(Cat, int);
							  public union Outer(Inner, string);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 24)
			.WithArguments("Outer O (field Inner)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task SelfReferentialUnionPass()
	{
		// U -> Box -> U is a cycle through the union's case types; it must terminate
		const string test = coGeneral
							+ """
							  public record A(U Value);

							  public struct Box { public U Inner; }
							  public union U(int, Box);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task GenericUnionPass()
	{
		const string test = coGeneral
							+ """
							  public record A(Option<int> O);

							  public record None;
							  public union Option<T>(None, T);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task GenericUnionFail()
	{
		// the case types come from the constructed type, so T = int[] fails
		const string test = coGeneral
							+ """
							  public record A(Option<int[]> O);

							  public record None;
							  public union Option<T>(None, T);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 32)
			.WithArguments("Option<int[]> O (field int[])");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericUnionOpenTypeParameterFail()
	{
		// an open T has no guaranteed equality, matching the existing rule for record Wrapper<T>(T Value)
		const string test = coGeneral
							+ """
							  public record Wrapper<T>(Option<T> O);

							  public record None;
							  public union Option<T>(None, T);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 26, 6, 37)
			.WithArguments("Option<T> O (field T)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericUnionTwoParametersPass()
	{
		const string test = coGeneral
							+ """
							  public record A(Either<int, string> E);

							  public union Either<L, R>(L, R);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task GenericUnionSecondParameterFail()
	{
		// substitution must reach every type parameter, not only the first
		const string test = coGeneral
							+ """
							  public record A(Either<int, Cat> E);

							  public class Cat { public string Name = ""; }
							  public union Either<L, R>(L, R);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 35)
			.WithArguments("Either<int, Cat> E (field Cat)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericUnionNullableValueTypeArgumentPass()
	{
		// the case type is int?, which unwraps to int
		const string test = coGeneral
							+ """
							  public record A(Option<int?> O);

							  public record None;
							  public union Option<T>(None, T);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task GenericUnionConstructedCaseFail()
	{
		// the case type List<T> is itself generic; T substitutes into it
		const string test = coGeneral
							+ """
							  public record A(Many<int> M);

							  public union Many<T>(T, List<T>);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 28)
			.WithArguments("Many<int> M (field System.Collections.Generic.List<int>)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericUnionOfGenericUnionPass()
	{
		const string test = coGeneral
							+ """
							  public record A(Option<Option<int>> O);

							  public record None;
							  public union Option<T>(None, T);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task GenericUnionOfGenericUnionFail()
	{
		// the outer report names the inner constructed union
		const string test = coGeneral
							+ """
							  public record A(Option<Option<int[]>> O);

							  public record None;
							  public union Option<T>(None, T);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 40)
			.WithArguments("Option<Option<int[]>> O (field Option<int[]>)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericUnionTwoConstructionsFail()
	{
		// each construction is checked on its own; a result keyed on the generic definition would report both or neither
		const string test = coGeneral
							+ """
							  public record A(Option<int> Good, Option<int[]> Bad);

							  public record None;
							  public union Option<T>(None, T);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 35, 6, 52)
			.WithArguments("Option<int[]> Bad (field int[])");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericSelfReferentialUnionPass()
	{
		// Tree<int> -> Node<int> -> Tree<int> is a cycle through constructed types; it must terminate
		const string test = coGeneral
							+ """
							  public record A(Tree<int> Root);

							  public struct Node<T> { public Tree<T> Next; }
							  public union Tree<T>(T, Node<T>);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task GenericCustomUnionStructFail()
	{
		// a hand-written generic [Union] struct takes its case types from the constructed constructors
		const string test = coGeneral
							+ """
							  public record A(Result<Cat> R);

							  public class Cat { public string Name = ""; }

							  [System.Runtime.CompilerServices.Union]
							  public struct Result<T> : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(T value) { Value = value; }
							  	public Result(string error) { Value = error; }
							  	public object Value { get; }
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 30)
			.WithArguments("Result<Cat> R (field Cat)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task CustomUnionStructPass()
	{
		// a hand-written [Union] struct: its case types are the single-parameter constructor types
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int value) { Value = value; }
							  	public Result(string value) { Value = value; }
							  	public object Value { get; }
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task CustomUnionStructClassCaseFail()
	{
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  public class Error { public string Message = ""; }

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int value) { Value = value; }
							  	public Result(Error value) { Value = value; }
							  	public object Value { get; }
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Result R (field Error)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task CustomUnionStructExtraFieldFail()
	{
		// ValueType.Equals compares every field, so non-union state still needs value semantics
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int value) { Value = value; Tags = new int[0]; }
							  	public object Value { get; }
							  	public int[] Tags;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Result R (field int[])");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task CustomUnionClassFail()
	{
		// a [Union] class without Equals compares by reference, whatever its case types
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public class Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int value) { Value = value; }
							  	public object Value { get; }
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Result R");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionWithEqualsPass()
	{
		// a user-defined Equals(T) on the union takes precedence over its case types
		const string test = coGeneral
							+ """
							  public record A(Pet P);

							  public class Cat { public string Name = ""; }
							  public record Dog(int Age);
							  public union Pet(Cat, Dog)
							  {
							  	public bool Equals(Pet other) => false;
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}
}
