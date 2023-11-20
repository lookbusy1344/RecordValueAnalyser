# Value-Semantics Analyser for C# Records

## TL;DR

Equality checks on .NET records don’t always work properly. This analyser reports when. For example:

```
record TestRecord(int A, string B, IReadOnlyList<int> C);
                                   ~~~~~~~~~~~~~~~~~~~~  JSV01: member lacks value semantics
```

## Repository

This project is a C# Roslyn code analyser to check records for correct value semantics. Source code is available here:

https://github.com/lookbusy1344/RecordValueAnalyser

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

It was built for C# 12 and .NET 8. It checks `record class` and `record struct` types for the following:

- if the record has a Equals(T) method, it is ok and no more checks are performed
- Otherwise all members are checked for:
    - the member is a primitive type, enum or string (these are ok)
    - it is a object or dynamic (these are never ok)
	- it is an inline array (these are never ok) - **new in version 1.2 for .NET 8**
    - it has Equals(T) or Equals(object) method overriden directly in the type (these are ok)
    - it is a record (these will be checked elsewhere, so are assumed ok here)
    - it is a class (without Equals method, these are not ok)
    - it is a tuple or struct (without Equals method, their members are checked recursively)

It works in Visual Studio 2022 and Visual Studio Code, and also on the command line.

## Warnings

- JSV01 - a record member lacks value semantics eg `record Test(IList<int> Fail)`

## Code fix

The analyser provides a simple code fix. It will add template `Equals` and `GetHashCode` methods to the member. For example:

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

It is not necessary for records to implement `IEquatable<T>`. When you write your implementations `SequenceEqual` is very useful for comparing  collections.
