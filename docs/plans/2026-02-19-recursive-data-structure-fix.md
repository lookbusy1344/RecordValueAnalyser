# Plan: Fix Infinite Recursion in CheckMember for Circular Type Layouts

**Date**: 2026-02-19
**Severity**: Medium — affects analyzer stability on malformed code
**Previous assessment**: The 2026-02-17 code review dismissed this as a non-issue, reasoning that CS0523 prevents circular struct layouts. That reasoning is incorrect because Roslyn analyzers run on code with compilation errors, and Roslyn creates fully-formed type symbols even for invalid code.

## Problem

`RecordValueSemantics.CheckMember()` recursively descends into struct members and tuple elements (line 100) with no cycle detection. When analysing code with circular struct layouts (which the compiler rejects with CS0523 but still creates symbols for), the analyzer enters unbounded recursion and stack-overflows.

### Reproduction scenario

```csharp
struct A { public B b; }
struct B { public A a; }
record R(A field);  // analyzer runs CheckMember(A) → CheckMember(B) → CheckMember(A) → ...
```

The compiler emits CS0523 but the analyzer still processes the record declaration. `CheckMember` passes all early-return guards for these struct types (they're not primitive, not records, not classes, have no Equals overrides) and reaches the recursive member enumeration.

### Why it matters

A stack overflow in a diagnostic analyzer crashes the user's IDE (Visual Studio, Rider) or build process. Analyzers must degrade gracefully on invalid code — they should never crash, even when the input is nonsensical.

## Fix

### Step 1: Add a `HashSet<ITypeSymbol>` visited-set parameter to CheckMember

Change the signature of the recursive `CheckMember` method to accept an optional visited set. The public entry point creates the set; the internal recursion passes it through.

```csharp
// Public entry point (unchanged call sites)
internal static CheckResultTuple CheckMember(ITypeSymbol? type)
    => CheckMember(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

// Private recursive implementation
private static CheckResultTuple CheckMember(ITypeSymbol? type, HashSet<ITypeSymbol> visited)
```

### Step 2: Add cycle detection at the start of the recursive method

After unwrapping the nullable and before any other checks, test-and-add the type to the visited set:

```csharp
type = GetUnderlyingType(type);
if (type == null)
    return (ValueEqualityResult.Ok, null);

// Cycle detection: if we've already seen this type, treat it as Ok
// to avoid infinite recursion on circular struct layouts (CS0523 code)
if (!visited.Add(type))
    return (ValueEqualityResult.Ok, null);
```

**Why return Ok on cycle?** A circular struct layout is already a compiler error. The analyzer's job is to report JSV01 (missing value semantics), not to duplicate CS0523. Returning Ok means the analyzer silently skips the circular reference rather than crashing. The alternative — returning Failed — would generate a potentially confusing JSV01 diagnostic on code that already has a more fundamental compilation error. Either choice is defensible; Ok is the less noisy option.

### Step 3: Thread the visited set through recursive calls

Update line 100:

```csharp
var (result, _) = CheckMember(memberType, visited);
```

### Step 4: Write tests

#### Test A: Direct circular struct — no crash, no diagnostic

```csharp
struct A { public B b; }
struct B { public A a; }
record R(A field);
```

Expected: No JSV01 diagnostic. The analyzer should complete without throwing.

#### Test B: Indirect circular struct through three types — no crash

```csharp
struct X { public Y y; }
struct Y { public Z z; }
struct Z { public X x; }
record R(X field);
```

Expected: No JSV01 diagnostic, no crash.

#### Test C: Circular struct where one member also has an invalid field — reports the non-circular issue

```csharp
struct A { public B b; public int[] Items; }
struct B { public A a; }
record R(A field);
```

Expected: JSV01 reported for `int[] Items` (the non-circular problem). The circular reference itself should not cause a crash.

### Step 5: Run full test suite

Verify no regressions. The visited set is created fresh per top-level `CheckMember` call, so it has no cross-call side effects.

### Step 6: Run `dotnet format`

Per project conventions.

## Files Modified

| File | Change |
|------|--------|
| `RecordValueAnalyser/RecordValueSemantics.cs` | Add visited-set overload, cycle detection |
| `RecordValueAnalyser.Test/UnitTestsClasses.cs` | Add circular struct tests (record class) |
| `RecordValueAnalyser.Test/UnitTestsStructs.cs` | Add circular struct tests (record struct) |

## Complexity

Low. The fix is ~10 lines of production code and ~60 lines of tests. The `HashSet` allocation per top-level call is negligible — it only exists during the analysis of a single member type.

## Alternatives Considered

1. **Recursion depth limit**: Simpler but arbitrary. A depth limit of (say) 20 would prevent stack overflow but wouldn't correctly identify the cause. It also risks false positives on legitimately deep (but non-circular) struct nesting.

2. **Check for CS0523 before recursing**: Would work but couples the analyzer to a specific error code. The visited-set approach is more robust and handles any unforeseen cycle scenarios.

3. **Do nothing**: The previous review's position. Unacceptable — analyzers must not crash on invalid code.
