# Value-Semantics Analyser for C# Records

[![CodeQL](https://github.com/lookbusy1344/RecordValueAnalyser/actions/workflows/codeql.yml/badge.svg)](https://github.com/lookbusy1344/RecordValueAnalyser/actions/workflows/codeql.yml)
[![Test](https://github.com/lookbusy1344/RecordValueAnalyser/actions/workflows/test.yml/badge.svg)](https://github.com/lookbusy1344/RecordValueAnalyser/actions/workflows/test.yml)

## TL;DR

Equality checks on .NET records don’t always work properly. This analyser reports when. For example:

```
record TestRecord(int A, string B, IReadOnlyList<int> C);
                                   ~~~~~~~~~~~~~~~~~~~~  JSV01: member lacks value semantics
```

## Contents

This project contains:
- the Roslyn code analyser itself
- a wrapper to make a nuget package https://www.nuget.org/packages/lookbusy1344.RecordValueAnalyser
- a VSIX plugin for Visual Studio 2022/6
- a test project

## Why?

This project is a C# Roslyn code analyser to check records for correct value semantics.

Records are a feature in modern C#. They are intended to be used for immutable data with value semantics. This means that two instances of the same record type should be considered equal if all their members are equal. This is the same as the behaviour of `struct` and tuple types.

Internally records are regular classes (or structs), but they have a synthesized `Equals` method that compares all their members. Without this Equals method, different instances would never be equal:

```
class TestClass
{
    public int A { get; set; }
    public string B { get; set; }
}

var a = new TestClass { A = 1, B = "Hello" };
var b = new TestClass { A = 1, B = "Hello" };
```

In this case `a` never equals `b`, because they are different instances. But with records:

```
record TestRecord(int A, string B);

var a = new TestRecord(1, "Hello");
var b = new TestRecord(1, "Hello");

```

Now `a` and `b` do equal, because the compiler has synthesized an `Equals` method that compares the members. This is more natural behaviour.

## But...

There is a gotcha. If one of your record members lacks value-semantics itself, the synthesized `Equals` method will not work correctly. For example:

```
record TestRecord(int A, string B, IReadOnlyList<int> C);

var a = new TestRecord(1, "Hello", new[] { 1, 2, 3 });
var b = new TestRecord(1, "Hello", new[] { 1, 2, 3 });

```

The C member is an array, and these lack value semantics. Therefore `a` and `b` do not equal any more!

## What the analyser does

It scans your records, and reports any members that don't have value semantics. It also scans into any nested stucts and tuples. In the above example, it would cause a warning on the C field:

```
record TestRecord(int A, string B, IReadOnlyList<int> C);
                                   ~~~~~~~~~~~~~~~~~~~~  JSV01: member lacks value semantics
```

It was built for C# 12+ and .NET 8, 9, and 10. It checks `record class` and `record struct` types for the following:

- if the record has a `Equals(T)` method, it is ok and no more checks are performed (partial records are handled correctly via the semantic model)
- Otherwise all members are checked for:
    - the member is a primitive type, enum or string (these are ok)
    - it is `object` or `dynamic` (these are never ok)
    - it is an inline array (these are never ok)
    - it is `ImmutableArray<T>` (these are never ok — compares array identity, not contents)
    - it is `ArraySegment<T>` (these are never ok — compares array identity, not contents)
    - it is `Memory<T>` or `ReadOnlyMemory<T>` (these are never ok — compare span identity, not contents)
    - it has `Equals(T)` or `Equals(object)` overridden directly in the type (these are ok)
    - it is a record (these will be checked elsewhere, so are assumed ok here)
    - it is a class (without Equals method, these are not ok)
    - it is a tuple or struct (without Equals method, their members are checked recursively)
    - circular struct layouts (CS0523) are detected and do not cause unbounded recursion
    - Multi-variable field declarations (e.g. `int[] A, B;`) each produce their own diagnostic

It works with:
    - Visual Studio 2022/6
    - Visual Studio Code
    - Rider
    - `dotnet` command line tools

## Warnings

- JSV01 - a record member lacks value semantics eg `record Test(IList<int> Fail)`

## Code fix

The analyser provides a simple code fix. It will add template `Equals` and `GetHashCode` methods to the record. For example:

```
public record class Test(IReadOnlyList<int> Numbers)
{
	public virtual bool Equals(Test? other) => false; // TODO
	public override int GetHashCode() => 0; // TODO
}
```

..or for record structs..

```
public record struct Test(IReadOnlyList<int> Numbers)
{
	public readonly bool Equals(Test other) => false; // TODO
	public override readonly int GetHashCode() => 0; // TODO
}
```

For derived record classes the correct modifier is emitted automatically:

```
// non-sealed derived record class
public record class Derived(IReadOnlyList<int> Numbers) : Base(Numbers)
{
	public new virtual bool Equals(Derived? other) => false; // TODO
	public override int GetHashCode() => 0; // TODO
}

// sealed derived record class
public sealed record class SealedDerived(IReadOnlyList<int> Numbers) : Base(Numbers)
{
	public new sealed bool Equals(SealedDerived? other) => false; // TODO
	public override int GetHashCode() => 0; // TODO
}
```

Each record in a hierarchy introduces a new type-specific `Equals(T?)` slot rather than overriding the base's `Equals(Base?)`, so `new` suppresses CS0114.

It is not necessary for records to implement `IEquatable<T>`. When you write your implementations `SequenceEqual` is very useful for comparing  collections.

### A note about GetHashCode for collections

GetHashCode implementation for collections is tricky!

```
public override int GetHashCode() => Numbers.GetHashCode(); // BROKEN!
public override int GetHashCode() => HashCode.Combine(Numbers); // BROKEN!

public override int GetHashCode() // CORRECT IMPLEMENTATION
{
	var hash = new HashCode();
	foreach (var n in Numbers) hash.Add(n);
	return hash.ToHashCode();
}

public readonly bool Equals(Test other) => Numbers.SequenceEqual(other.Numbers); // CORRECT IMPLEMENTATION
```

## Testing

[![Test](https://github.com/lookbusy1344/RecordValueAnalyser/actions/workflows/test.yml/badge.svg)](https://github.com/lookbusy1344/RecordValueAnalyser/actions/workflows/test.yml)

Tests are in `RecordValueAnalyser.Test`. They are run in the CI pipeline.

## Usage

Install from nuget:

https://www.nuget.org/packages/lookbusy1344.RecordValueAnalyser

..or from source, edit your csproj file:

```
<ProjectReference Include="Path\To\RecordAnalyser.csproj" OutputItemType="Analyzer" 
    ReferenceOutputAssembly="false" />

```

Thanks to Thomas for helping with nuget packaging. https://github.com/elmahio/CronExpressions

## Examples

These are taken from the test project. Members expected to pass are named `Pass`, and those expected to fail are named `Fail`.

```
// these check members, called parameters in Roslyn

public record class A(F FooFail, G BarFail, string SPass, StructA SaFail);

public record class B(int IPass, IReadOnlyList<int> JFail, int? NullableIntPass, StructB BaPass);

public record struct AS(F FooFail, G GShouldFail, H HShouldPass, string SPass, Inner InnPass, object OFail);

public record struct Tup1(int IPass, (int a, int b) TupPass, DateTime? DtPass);

public record struct Tup2(int IPass, (int a, int[] b, object o) TupFail);

public record struct Tup3(int IPass, (bool, int) TupPass);

// this checks fields and properties
public record class RecFields(int IPass, string SPass, object OFail)
{
	public IList<string>? FieldFail;
	public int[]? PropertyFail { get; set; }

	public int FieldPass;
	public string? PropertyPass { get; set; }

    //uncomment, and the failures will disappear
	//public virtual bool Equals(RecFields? other) => true;

	public override int GetHashCode() => 0; // TODO
}

// the record class has an Equals method, so its assumed to be ok
public record class HasEqualsRecordClass(IReadOnlyList<int> NumsPass)
{
	public virtual bool Equals(HasEqualsRecordClass? other) => other != null && NumsPass.SequenceEqual(other.NumsPass);

	public override int GetHashCode() => 0; // TODO
}

// the record struct has an Equals method, so its assumed to be ok
public record struct HasEqualsRecordStruct(IReadOnlyList<int> NumsPass)
{
	public readonly bool Equals(HasEqualsRecordStruct other) => NumsPass.SequenceEqual(other.NumsPass);

	public override readonly int GetHashCode() => 0; // TODO
}

// ============= Supporting types =============

// this is nested inside another record
public record Inner(int IPass, string JPass, DateTime DtPass);

// when used in record, this fails because it is a class with no Equals method
public class F { public int[]? n; }

// when used in record, this fails because it is a class with no Equals method
public class G { public int i; }

// when used in record, this passes because it has Equals(T)
public class H
{
	public int i;

	public bool Equals(H? other) => other != null && other.i == this.i;

	//public override bool Equals(object? obj) => Equals(obj as H);

	public override int GetHashCode() => 0; // TODO
}

// when used in a record, this fails because no Equals method
public struct StructA { public int[] Numbers; }

// when used in a record, this passes because its fields have value semantics
public struct StructB { public int A; public string S; }
```
