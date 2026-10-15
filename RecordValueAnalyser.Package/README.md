# Value-Semantics Analyser for C# Records

[![CodeQL](https://github.com/lookbusy1344/RecordValueAnalyser/actions/workflows/codeql.yml/badge.svg)](https://github.com/lookbusy1344/RecordValueAnalyser/actions/workflows/codeql.yml)
[![Test](https://github.com/lookbusy1344/RecordValueAnalyser/actions/workflows/test.yml/badge.svg)](https://github.com/lookbusy1344/RecordValueAnalyser/actions/workflows/test.yml)

## TL;DR

Equality checks on .NET records don't always work properly. This analyser reports when. For example:

```
record TestRecord(int A, string B, IReadOnlyList<int> C);
                                   ~~~~~~~~~~~~~~~~~~~~  JSV01: member lacks value semantics
```

## Repository

Source code and issue tracker: https://github.com/lookbusy1344/RecordValueAnalyser

## Supported versions

The package holds two builds of the analyser, and the compiler loads the newest one it supports. Union type checks are only in the Roslyn 5.9 build.

| Toolchain | Default C# | Analyser build | Union type checks |
|---|---|---|---|
| .NET 8 SDK | 12 | Roslyn 4.8 | no |
| .NET 9 SDK | 13 | Roslyn 4.8 | no |
| .NET 10 SDK with a compiler older than Roslyn 5.9 | 14 | Roslyn 4.8 | no |
| .NET 10 SDK with Roslyn 5.9 or newer (e.g. 10.0.402) | 14 | Roslyn 5.9 | yes |
| .NET 11 SDK | 15 | Roslyn 5.9 | yes |
| Visual Studio 2022 17.8 or newer | 12–13 | Roslyn 4.8 | no |
| Visual Studio 2026 | 14–15 | depends on its compiler, as for the .NET 10 SDK | with the Roslyn 5.9 build |

CI packs the package and checks that the .NET 8, 9, 10 and 11 SDKs each load the expected build and report JSV01. The VSIX needs Visual Studio 2026.

## Why?

This project is a C# Roslyn code analyser to check records for correct value semantics.

Records are a feature in modern C#. They are intended to be used for immutable data with value semantics. This means that two instances of the same
record type should be considered equal if all their members are equal. This is the same as the behaviour of `struct` and tuple types.

Internally records are regular classes (or structs), but they have a synthesized `Equals` method that compares all their members. Without this Equals
method, different instances would never be equal:

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

It scans your records, and reports any members that don't have value semantics. It also scans into any nested structs and tuples. In the above example,
it would cause a warning on the C field:

```
record TestRecord(int A, string B, IReadOnlyList<int> C);
                                   ~~~~~~~~~~~~~~~~~~~~  JSV01: member lacks value semantics
```

It works with .NET 8, 9, 10 and 11 (see [Supported versions](#supported-versions)). It checks `record class` and `record struct` types for the following:

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
    - it is a C# 15 union type. A union type compares its case through `object.Equals`, so it has value semantics when every case type has them:
        - each case type is checked recursively, along with any other fields. Case types come from the public single-parameter constructors, or from the static `Create` methods of a nested `IUnionMembers` interface
        - a union type that is a class, without an Equals method, compares by reference and fails like any class
        - a record that is itself a union type is checked through its case types
        - the Roslyn 4.8 build does not recognise union types, and checks them as ordinary structs or classes
    - circular struct layouts (CS0523) are detected and do not cause unbounded recursion
    - Multi-variable field declarations (e.g. `int[] A, B;`) each produce their own diagnostic

It works with:

- Visual Studio 2022 and 2026 (the VSIX needs 2026)
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

It is not necessary for records to implement `IEquatable<T>`.
