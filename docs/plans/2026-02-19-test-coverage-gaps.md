# Test Coverage Gaps — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add 11 missing tests that cover untested code paths and document analyzer behaviour at boundary conditions.

**Architecture:** All tests follow the existing MSTest + Roslyn analyzer testing pattern. Each test supplies C# source as a string, declares expected diagnostics (if any), and calls `VerifyCS.VerifyAnalyzerAsync`. No production code changes are required; all gaps are missing test coverage, not analyzer bugs.

**Tech Stack:** MSTest, `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.MSTest`, C# 12 raw string literals, `gtimeout` for all `dotnet test` invocations.

---

## Background: confirmed gaps

| # | Gap | Why it matters |
|---|-----|----------------|
| 1 | `ImmutableArray<T>` in record **struct** | Only tested for `record class`; parity missing |
| 2 | Generic `T : class` constraint | None of the three generic tests covers this constraint |
| 3 | Generic `T : notnull` constraint | As above — different constraint, same gap |
| 4 | `static bool Equals(T)` in nested struct | `HasEqualsTMethod` has `!m.IsStatic` guard; never exercised |
| 5 | Non-`override` `Equals(object)` in nested struct | `HasEqualsObjectMethod` has `m.IsOverride` guard; never exercised |
| 6 | `nint` / `nuint` native integers | Not in `IsPrimitiveType`; resolves via `HasEqualsTMethod` — behaviour undocumented |
| 7 | `[Flags]` enum | `HasSimpleEquality` checks `TypeKind.Enum`; [Flags] is still an enum — not verified |
| 8 | Enum with non-default underlying type | Same path as above, different sub-case |
| 9 | Nested tuple `(int, (string, int[]))` | Recursive tuple descent is tested one level deep only |
| 10 | Nested tuple all-pass `(int, (string, DateTime))` | Positive counterpart to above |
| 11 | `T : class` in `record class` (class parity for gaps 2–3) | Generic tests in classes are also missing these constraints |

---

## Conventions

- Test file for record structs: `RecordValueAnalyser.Test/UnitTestsStructs.cs`
- Test file for record classes: `RecordValueAnalyser.Test/UnitTestsClasses.cs`
- `coGeneral` in each file equals `TestConstants.General` — provides `using System;`, `using System.Collections.Generic;`, and an `IsExternalInit` stub. The string starts with `\n`, so appended code lands on **line 6**.
- Run all tests with: `gtimeout 120 dotnet test RecordValueAnalyser.Test`
- Run a single test with: `gtimeout 60 dotnet test RecordValueAnalyser.Test --filter "Name=<MethodName>"`
- `WithSpan(line, startCol, line, endCol)` uses 1-indexed columns. **Omit `WithSpan` on first write**; get the actual span from the test failure message, then add it.
- After adding `WithSpan`, re-run the test to confirm it still passes.

---

## Task 1 — ImmutableArray\<T\> in record struct

**File:** `RecordValueAnalyser.Test/UnitTestsStructs.cs`

`IsImmutableArrayType()` is exercised by the existing class test but not the struct test. This is a pure parity gap.

### Step 1: Write the failing test

Add after the `ReadOnlyMemoryFail` test method (around line 400):

```csharp
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
		.WithArguments("System.Collections.Immutable.ImmutableArray<int> Numbers");

	await VerifyCS.VerifyAnalyzerAsync(test, expected);
}
```

### Step 2: Run test to verify it behaves correctly

```
gtimeout 60 dotnet test RecordValueAnalyser.Test --filter "Name=RecordWithImmutableArray"
```

If the test passes, proceed. If the test fails because the analyzer does NOT flag `ImmutableArray` in a struct context (unexpected), that is an analyzer bug — open a separate issue.

### Step 3: Add WithSpan for precision

The class version uses `WithSpan(6, 35, 6, 91)`. The struct version is one char wider (`struct` vs `class`), so the span shifts one column right:

```csharp
.WithSpan(6, 36, 6, 92)
```

Replace `WithArguments(...)` with:
```csharp
.WithSpan(6, 36, 6, 92)
.WithArguments("System.Collections.Immutable.ImmutableArray<int> Numbers");
```

### Step 4: Run test again to confirm span is correct

```
gtimeout 60 dotnet test RecordValueAnalyser.Test --filter "Name=RecordWithImmutableArray"
```

Expected: PASS

### Step 5: Format and commit

```bash
dotnet format RecordValueAnalyser.Test
git add RecordValueAnalyser.Test/UnitTestsStructs.cs
git commit -m "test: add ImmutableArray struct parity test"
```

---

## Task 2 — Generic T : class and T : notnull constraints

**Files:** `UnitTestsStructs.cs` and `UnitTestsClasses.cs`

The three existing generic tests cover unconstrained `T`, `T : struct`, and `T : IEquatable<T>`. `T : class` and `T : notnull` are missing from both files. All generic type parameters fall through `CheckMember` to `return (Failed, null)` because they have no members, are not records, and are not structs.

### Step 1: Write the four failing tests

**In `UnitTestsStructs.cs`**, add after `GenericRecordIEquatableConstrainedFail`:

```csharp
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
```

**In `UnitTestsClasses.cs`**, add after `GenericRecordIEquatableConstrainedFail` (spans shift one column left due to `class` vs `struct`):

```csharp
[TestMethod]
public async Task GenericRecordClassConstrainedFail()
{
	// T : class still falls through to Failed — class-constrained T has no guaranteed Equals(T)
	const string test = coGeneral + "public record class Wrapper<T>(T Value) where T : class;";

	var expected = VerifyCS.Diagnostic("JSV01")
		.WithSpan(6, 32, 6, 39)
		.WithArguments("T Value");

	await VerifyCS.VerifyAnalyzerAsync(test, expected);
}

[TestMethod]
public async Task GenericRecordNotNullConstrainedFail()
{
	// T : notnull still falls through to Failed — notnull does not imply value semantics
	const string test = coGeneral + "public record class Wrapper<T>(T Value) where T : notnull;";

	var expected = VerifyCS.Diagnostic("JSV01")
		.WithSpan(6, 32, 6, 39)
		.WithArguments("T Value");

	await VerifyCS.VerifyAnalyzerAsync(test, expected);
}
```

### Step 2: Run new tests

```
gtimeout 60 dotnet test RecordValueAnalyser.Test --filter "Name=GenericRecordClassConstrainedFail|Name=GenericRecordNotNullConstrainedFail"
```

Expected: all four PASS.

### Step 3: Format and commit

```bash
dotnet format RecordValueAnalyser.Test
git add RecordValueAnalyser.Test/UnitTestsStructs.cs RecordValueAnalyser.Test/UnitTestsClasses.cs
git commit -m "test: add generic T : class and T : notnull constraint scenarios"
```

---

## Task 3 — Equals method boundary conditions in nested structs

**File:** `RecordValueAnalyser.Test/UnitTestsStructs.cs`

`HasEqualsTMethod` explicitly checks `!m.IsStatic` (line 260, `RecordValueSemantics.cs`). `HasEqualsObjectMethod` checks `m.IsOverride` (line 272). Neither guard has a test verifying that the wrong-signature Equals does NOT suppress the diagnostic.

### Step 1: Write the two failing tests

Add after `NestedStructBInvalidEqualsFail`:

```csharp
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
		.WithArguments("StructA Sa (field int[])");

	await VerifyCS.VerifyAnalyzerAsync(test, expected);
}
```

### Step 2: Run tests

```
gtimeout 60 dotnet test RecordValueAnalyser.Test --filter "Name=NestedStructStaticEqualsFail|Name=NestedStructNonOverrideEqualsObjectFail"
```

Expected: both PASS.

If `NestedStructNonOverrideEqualsObjectFail` unexpectedly passes (no diagnostic raised), the analyzer has a bug: a non-override `Equals(object)` is incorrectly accepted. Investigate `HasEqualsObjectMethod` in `RecordValueSemantics.cs`.

### Step 3: Add WithSpan after verifying

Run each test individually with a minimal version first (no `WithSpan`), note the span in the output, then add `WithSpan(line, startCol, line, endCol)` and re-run.

For this test structure, `public record struct A(StructA Sa);` lands several lines into the raw string. The exact line depends on indentation trimming. Get the span from the test runner output.

### Step 4: Format and commit

```bash
dotnet format RecordValueAnalyser.Test
git add RecordValueAnalyser.Test/UnitTestsStructs.cs
git commit -m "test: verify static and non-override Equals do not suppress JSV01"
```

---

## Task 4 — Native integer types (nint / nuint)

**File:** `RecordValueAnalyser.Test/UnitTestsStructs.cs`

`nint` and `nuint` map to `System.IntPtr` / `System.UIntPtr`. These are **not** in `IsPrimitiveType`'s switch, so they fall through to `HasEqualsTMethod`. In .NET 5+, `IntPtr` implements `IEquatable<IntPtr>`, so `HasEqualsTMethod` returns `true` and they pass. This path is untested.

### Step 1: Write the test

Add after `DecimalMemberPass`:

```csharp
[TestMethod]
public async Task NativeIntegerPass()
{
	// nint/nuint are not in IsPrimitiveType but IntPtr/UIntPtr implement IEquatable<T> — should pass
	const string test = coGeneral + "public record struct A(nint I, nuint U);";

	await VerifyCS.VerifyAnalyzerAsync(test);
}
```

### Step 2: Run test

```
gtimeout 60 dotnet test RecordValueAnalyser.Test --filter "Name=NativeIntegerPass"
```

Expected: PASS (no diagnostics).

If the test unexpectedly fails with a diagnostic on `nint` or `nuint`, it means `IntPtr` does not expose `Equals(IntPtr)` as a non-static non-override member in the test runtime's Roslyn symbol table. This would be a documentation finding — rename the test to `NativeIntegerFail`, add the expected diagnostic, and add a comment explaining the behaviour.

### Step 3: Format and commit

```bash
dotnet format RecordValueAnalyser.Test
git add RecordValueAnalyser.Test/UnitTestsStructs.cs
git commit -m "test: document nint/nuint pass via HasEqualsTMethod"
```

---

## Task 5 — Enum type variations

**File:** `RecordValueAnalyser.Test/UnitTestsStructs.cs`

`HasSimpleEquality` checks `type.TypeKind == TypeKind.Enum`. Both `[Flags]` enums and enums with non-default underlying types still have `TypeKind.Enum`. Neither variant is currently tested.

### Step 1: Write the two tests

Add after `DecimalMemberPass` (or after the `NativeIntegerPass` from Task 4):

```csharp
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
```

### Step 2: Run tests

```
gtimeout 60 dotnet test RecordValueAnalyser.Test --filter "Name=FlagsEnumPass|Name=EnumWithByteUnderlyingTypePass"
```

Expected: both PASS.

### Step 3: Format and commit

```bash
dotnet format RecordValueAnalyser.Test
git add RecordValueAnalyser.Test/UnitTestsStructs.cs
git commit -m "test: verify [Flags] enum and byte-underlying enum have simple equality"
```

---

## Task 6 — Nested tuple recursion

**File:** `RecordValueAnalyser.Test/UnitTestsStructs.cs`

`RecordWithTupleFail` tests a tuple containing `int[]` directly. Recursive descent into a tuple-within-a-tuple `(int, (string, int[]))` is untested. The failure propagation returns the failing inner member's containing type, not the deepest bad field.

Expected diagnostic args for `(int, (string, int[])) T`:
- The inner tuple `(string, int[])` fails because `int[]` is a class
- The outer tuple's loop discards the inner error name and reports the inner tuple's type as the `errorMember`
- Result: `"(int, (string, int[])) T (field (string, int[]))"`

### Step 1: Write the two tests

Add after `RecordWithTupleFail`:

```csharp
[TestMethod]
public async Task RecordWithNestedTupleFail()
{
	// recursive descent into nested tuple — outer loop reports the inner tuple as the failing member
	const string test = coGeneral + "public record struct A((int, (string, int[])) T);";

	var expected = VerifyCS.Diagnostic("JSV01")
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
```

### Step 2: Run tests

```
gtimeout 60 dotnet test RecordValueAnalyser.Test --filter "Name=RecordWithNestedTupleFail|Name=RecordWithNestedTuplePass"
```

Expected: both PASS.

If `RecordWithNestedTupleFail` fails with different diagnostic args, copy the actual args from the test output and update the test. The args string reflects exactly how the analyzer surfaces nested failures — documenting the actual output is the goal.

### Step 3: Add WithSpan after verifying

Get the actual span from the first run and add `.WithSpan(6, startCol, 6, endCol)` to the fail test.

For `"public record struct A((int, (string, int[])) T);"` on line 6:
- `public record struct A(` = 23 chars → param starts at col 24
- `(int, (string, int[])) T` = 24 chars → ends at col 48

Expected: `.WithSpan(6, 24, 6, 48)` — confirm from test output.

### Step 4: Format and commit

```bash
dotnet format RecordValueAnalyser.Test
git add RecordValueAnalyser.Test/UnitTestsStructs.cs
git commit -m "test: add nested tuple recursion pass and fail scenarios"
```

---

## Final verification

Run the full test suite to confirm all new tests pass and nothing was broken:

```
gtimeout 120 dotnet test RecordValueAnalyser.Test
```

Expected: all tests PASS, count increases by 11.

---

## Out of scope (deliberately excluded)

- **Multi-dimensional arrays** (`int[,]`, `int[][]`): these are reference types (`IsClassType → true`) and are handled identically to `int[]`. No new code path is exercised.
- **`Span<T>` / `ReadOnlySpan<T>`**: ref structs; the analyzer is not expected to handle them specially — they cannot be stored in record fields in practice.
- **Recursive self-referential structs** (`struct Node { public Node? Next; }`): the unwrap-nullable path strips the `?`, leading to an `ITypeSymbol` for `Node` that would recurse infinitely. This would require a cycle-detection fix in the analyzer before a test could be written.
- **Record `Equals` with `static` modifier**: C# does not allow a static method with the same signature as the synthesised `bool Equals(T)` in a record — the compiler rejects it before the analyzer runs.
