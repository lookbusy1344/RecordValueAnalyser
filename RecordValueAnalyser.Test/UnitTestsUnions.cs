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

	[TestMethod]
	public async Task UnionPrivateConstructorPass()
	{
		// a private constructor chaining to a case constructor is not a case, so Cat[] is not checked
		const string test = coGeneral
							+ """
							  public record A(Pet P);

							  public record Cat(string Name);
							  public record Dog(int Age);
							  public union Pet(Cat, Dog)
							  {
							  	private Pet(Cat[] cats) : this(cats[0]) { }
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task CustomUnionStructPrivateConstructorPass()
	{
		// only public constructors are case constructors; the private object constructor is a helper
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	private Result(object value, bool unused) { Value = value; }
							  	private Result(object value) { Value = value; }
							  	internal Result(int[] value) { Value = value; }
							  	public Result(int value) : this((object)value) { }
							  	public object Value { get; }
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task CustomUnionStructMultiParameterConstructorPass()
	{
		// a constructor with two parameters is not a case constructor, so int[] is not a case type
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int value) { Value = value; }
							  	public Result(int[] values, int index) : this(values[index]) { }
							  	public object Value { get; }
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task CustomUnionStructRefConstructorPass()
	{
		// a case constructor takes its parameter by value or 'in'; ref and out constructors are not cases
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int value) { Value = value; }
							  	public Result(ref int[] values) : this(values[0]) { }
							  	public Result(out string[] values) : this(0) { values = new string[0]; }
							  	public object Value { get; }
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task CustomUnionStructInConstructorFail()
	{
		// an 'in' parameter is a case constructor
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int value) { Value = value; }
							  	public Result(in int[] values) { Value = values; }
							  	public object Value { get; }
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Result R (field int[])");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task CustomUnionStructExtraObjectFieldFail()
	{
		// only the case storage is checked through the case types; another object field is compared with object.Equals
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int value) { Value = value; Tag = null; }
							  	public object Value { get; }
							  	public object Tag;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Result R (field object)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task CustomUnionStructExtraPrivateObjectFieldFail()
	{
		// Value is an auto-property, so a private object field is extra state, not the case storage
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	private object _tag;
							  	public Result(int value) { Value = value; _tag = null; }
							  	public object Value { get; }
							  	public object GetTag() => _tag;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Result R (field object)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task CustomUnionStructExplicitStorageFieldPass()
	{
		// Value reads an explicit object field; that field is the case storage
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	private readonly object _value;
							  	public Result(int value) { _value = value; }
							  	public Result(string value) { _value = value; }
							  	public object Value => _value;
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task CustomUnionStructTwoObjectFieldsFail()
	{
		// with two object fields the case storage is ambiguous, so both are checked
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	private readonly object _value;
							  	private readonly object _tag;
							  	public Result(int value) { _value = value; _tag = null; }
							  	public object Value => _value;
							  	public object GetTag() => _tag;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Result R (field object)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionMemberProviderPass()
	{
		// a nested IUnionMembers interface supplies the case types through static Create methods
		const string test = coGeneral
							+ """
							  public record A(Result<int> R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result<T> : Result<T>.IUnionMembers
							  {
							  	private object _value;

							  	public interface IUnionMembers
							  	{
							  		public static Result<T> Create(T value) => new() { _value = value };
							  		public static Result<T> Create(string error) => new() { _value = error };
							  		object Value { get; }
							  	}

							  	object IUnionMembers.Value => _value;
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task UnionMemberProviderGenericFail()
	{
		// the Create parameter types come from the constructed union, so T = Cat fails
		const string test = coGeneral
							+ """
							  public record A(Result<Cat> R);

							  public class Cat { public string Name = ""; }

							  [System.Runtime.CompilerServices.Union]
							  public struct Result<T> : Result<T>.IUnionMembers
							  {
							  	private object _value;

							  	public interface IUnionMembers
							  	{
							  		public static Result<T> Create(T value) => new() { _value = value };
							  		public static Result<T> Create(string error) => new() { _value = error };
							  		object Value { get; }
							  	}

							  	object IUnionMembers.Value => _value;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 30)
			.WithArguments("Result<Cat> R (field Cat)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionMemberProviderClassCaseFail()
	{
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  public class Error { public string Message = ""; }

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : Result.IUnionMembers
							  {
							  	private object _value;

							  	public interface IUnionMembers
							  	{
							  		public static Result Create(int value) => new() { _value = value };
							  		public static Result Create(Error error) => new() { _value = error };
							  		object Value { get; }
							  	}

							  	object IUnionMembers.Value => _value;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Result R (field Error)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionMemberProviderInheritedCreateFail()
	{
		// Create methods on an interface that IUnionMembers inherits are also union factory methods
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  public class Error { public string Message = ""; }

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : Result.IUnionMembers
							  {
							  	private object _value;

							  	public interface IBaseMembers
							  	{
							  		public static Result Create(Error error) => new() { _value = error };
							  	}

							  	public interface IUnionMembers : IBaseMembers
							  	{
							  		public static Result Create(int value) => new() { _value = value };
							  		object Value { get; }
							  	}

							  	object IUnionMembers.Value => _value;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Result R (field Error)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionMemberProviderIgnoresOtherMembersPass()
	{
		// with a provider, constructors on the union are not cases; nor are Create methods that are private, instance, return another type or take two parameters
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : Result.IUnionMembers
							  {
							  	private object _value;

							  	public Result(int[] values) { _value = values[0]; }

							  	public interface IUnionMembers
							  	{
							  		public static Result Create(int value) => new() { _value = value };
							  		private static Result Create(string[] values) => new() { _value = values[0] };
							  		public static int Create(int[] values) => values[0];
							  		public static Result Create(int[] values, int index) => new() { _value = values[index] };
							  		public Result Create(List<int> values) => new() { _value = values };
							  		object Value { get; }
							  	}

							  	object IUnionMembers.Value => _value;
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task UnionMemberProviderNotImplementedUsesConstructors()
	{
		// a nested IUnionMembers the union does not implement is not a provider, so the constructors define the cases
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int value) { Value = value; }
							  	public Result(int[] values) { Value = values; }
							  	public object Value { get; }

							  	public interface IUnionMembers
							  	{
							  		public static Result Create(int value) => new(value);
							  	}
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Result R (field int[])");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionInStructFieldFail()
	{
		// a union reached through a plain struct field is checked by its case types
		const string test = coGeneral
							+ """
							  public record A(Holder H);

							  public struct Holder { public Pet P; }
							  public class Cat { public string Name = ""; }
							  public record Dog(int Age);
							  public union Pet(Cat, Dog);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 25)
			.WithArguments("Holder H (field Pet)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionInTupleFail()
	{
		const string test = coGeneral
							+ """
							  public record A((Pet, int) T);

							  public class Cat { public string Name = ""; }
							  public record Dog(int Age);
							  public union Pet(Cat, Dog);
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(6, 17, 6, 29)
			.WithArguments("(Pet, int) T (field Pet)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionInTuplePass()
	{
		const string test = coGeneral
							+ """
							  public record A((Pet, int) T);

							  public record Cat(string Name);
							  public record Dog(int Age);
							  public union Pet(Cat, Dog);
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task StructUnionStaticObjectFieldPass()
	{
		// ValueType.Equals ignores static fields, so a static sentinel is neither state nor ambiguous case storage
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	private static readonly object Sentinel = new object();
							  	private readonly object _value;
							  	public Result(int value) { _value = value; }
							  	public object Value => _value == Sentinel ? null : _value;
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task UnionComputedObjectPropertyPass()
	{
		// a computed property holds no state, so ValueType.Equals never compares it
		const string test = coGeneral
							+ """
							  public record A(Pet P);

							  public record Cat(string Name);
							  public union Pet(Cat, int)
							  {
							  	public object Boxed => Value;
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task RecordClassUnionPass()
	{
		// a record class union compares its case field with object.Equals; int and string have value semantics
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public record class Result : Result.IUnionMembers
							  {
							  	private readonly object _value;
							  	private Result(object value) { _value = value; }

							  	public interface IUnionMembers
							  	{
							  		public static Result Create(int value) => new(value);
							  		public static Result Create(string value) => new(value);
							  		object Value { get; }
							  	}

							  	object IUnionMembers.Value => _value;
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task RecordClassUnionBadCaseFail()
	{
		// the case field compares int[] by reference; reported where the union is declared, not where it is used
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public record class Result : Result.IUnionMembers
							  {
							  	private readonly object _value;
							  	private Result(object value) { _value = value; }

							  	public interface IUnionMembers
							  	{
							  		public static Result Create(int value) => new(value);
							  		public static Result Create(int[] value) => new(value);
							  		object Value { get; }
							  	}

							  	object IUnionMembers.Value => _value;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(11, 2, 11, 33)
			.WithArguments("object _value (case int[])");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task GenericRecordClassUnionFail()
	{
		// an open case type T has no guaranteed equality, matching record Wrapper<T>(T Value)
		const string test = coGeneral
							+ """
							  public record A(Result<int[]> R);

							  [System.Runtime.CompilerServices.Union]
							  public record class Result<T> : Result<T>.IUnionMembers
							  {
							  	private readonly object _value;
							  	private Result(object value) { _value = value; }

							  	public interface IUnionMembers
							  	{
							  		public static Result<T> Create(string error) => new(error);
							  		public static Result<T> Create(T value) => new(value);
							  		object Value { get; }
							  	}

							  	object IUnionMembers.Value => _value;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(11, 2, 11, 33)
			.WithArguments("object _value (case T)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task RecordStructUnionPass()
	{
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public record struct Result : System.Runtime.CompilerServices.IUnion
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
	public async Task RecordStructUnionBadCaseFail()
	{
		// an auto-property Value holds the case, so it is reported with the failing case type
		const string test = coGeneral
							+ """
							  public record A(Result R);

							  [System.Runtime.CompilerServices.Union]
							  public record struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int[] value) { Value = value; }
							  	public object Value { get; }
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(12, 2, 12, 30)
			.WithArguments("object Value (case int[])");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task RecordUnionExtraObjectFieldFail()
	{
		// only the case storage is checked through the case types; other record fields are checked as usual
		const string test = coGeneral
							+ """
							  [System.Runtime.CompilerServices.Union]
							  public record struct Result : System.Runtime.CompilerServices.IUnion
							  {
							  	public Result(int value) { Value = value; Tag = null; }
							  	public object Value { get; }
							  	public object Tag;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(11, 2, 11, 20)
			.WithArguments("object Tag");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task RecordUnionPrimaryConstructorFail()
	{
		// the positional parameter is reported once; the computed Value only reads it
		const string test = coGeneral
							+ """
							  [System.Runtime.CompilerServices.Union]
							  public record class Result(int[] Items) : System.Runtime.CompilerServices.IUnion
							  {
							  	public object Value => Items;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(7, 28, 7, 39)
			.WithArguments("int[] Items");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task UnionMemberProviderNullableTypeParameterPass()
	{
		// Create(T? value) with T = int has case type int, annotated but not Nullable<int>
		const string test = coGeneral
							+ """
							  #nullable enable
							  public record A(Outcome<int> O);

							  [System.Runtime.CompilerServices.Union]
							  public struct Outcome<T> : Outcome<T>.IUnionMembers
							  {
							  	private readonly object? _value;
							  	private Outcome(object? value) => _value = value;

							  	public interface IUnionMembers
							  	{
							  		static Outcome<T> Create(T? value) => new(value);
							  		static Outcome<T> Create(string? value) => new(value);
							  		object? Value { get; }
							  	}

							  	object? IUnionMembers.Value => _value;
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}

	[TestMethod]
	public async Task UnionMemberProviderNullableTypeParameterFail()
	{
		// the Outcome<T> sample from the union docs: Exception compares by reference
		const string test = coGeneral
							+ """
							  #nullable enable
							  public record A(Outcome<int> O);

							  [System.Runtime.CompilerServices.Union]
							  public struct Outcome<T> : Outcome<T>.IUnionMembers
							  {
							  	private readonly object? _value;
							  	private Outcome(object? value) => _value = value;

							  	public interface IUnionMembers
							  	{
							  		static Outcome<T> Create(T? value) => new(value);
							  		static Outcome<T> Create(Exception? value) => new(value);
							  		object? Value { get; }
							  	}

							  	object? IUnionMembers.Value => _value;
							  }
							  """
							+ coUnionTypes;

		var expected = VerifyCS.Diagnostic("JSV01")
			.WithSpan(7, 17, 7, 31)
			.WithArguments("Outcome<int> O (field System.Exception)");

		await VerifyPreviewAsync(test, expected);
	}

	[TestMethod]
	public async Task NonBoxingUnionPass()
	{
		// the IntOrBool sample from the union docs: typed storage, Nullable<T> case types, computed Value
		const string test = coGeneral
							+ """
							  public record A(IntOrBool V);

							  [System.Runtime.CompilerServices.Union]
							  public struct IntOrBool : System.Runtime.CompilerServices.IUnion
							  {
							  	private readonly int _intValue;
							  	private readonly bool _boolValue;
							  	private readonly byte _tag;
							  	public IntOrBool(int? value) { if (value.HasValue) { _intValue = value.Value; _tag = 1; } }
							  	public IntOrBool(bool? value) { if (value.HasValue) { _boolValue = value.Value; _tag = 2; } }
							  	public object Value => _tag switch { 1 => _intValue, 2 => _boolValue, _ => null };
							  	public bool HasValue => _tag != 0;
							  	public bool TryGetValue(out int value) { value = _intValue; return _tag == 1; }
							  	public bool TryGetValue(out bool value) { value = _boolValue; return _tag == 2; }
							  }
							  """
							+ coUnionTypes;

		await VerifyPreviewAsync(test);
	}
}
