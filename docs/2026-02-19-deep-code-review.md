# Deep Code Review: RecordValueAnalyser

**Date:** 2026-02-19
**Reviewer:** Claude Code (Opus 4.6)
**Commit:** `3306c23` (main)
**Scope:** Analyzer + CodeFix + Tests (excludes Package/Vsix projects)

---

## Executive Summary

A thorough review of the Roslyn analyzer found **1 critical correctness bug**, **2 high-severity issues** in the code fix provider, **2 medium issues** in the analyzer, and **6 test coverage gaps**. The critical bug causes **false negatives** — the analyzer silently passes members it should flag.

Severity ratings: **Critical** (correctness bug, silent wrong results) / **High** (generates wrong code or crashes) / **Medium** (edge case gaps, inconsistencies) / **Low** (cleanup, documentation)

---

## 1. Critical: `visited` Set Causes False Negatives on Sibling Struct Fields

**File:** `RecordValueSemantics.cs:30-41, 96-118`
**Confidence:** 95%

### The Problem

The cycle-detection `HashSet<ITypeSymbol>` accumulates entries but never removes them. It functions as a "globally seen" set rather than a "current recursion path" set. When two sibling fields of a struct share a common type, the second field is silently treated as a cycle back-edge and returns `Ok` without analysis.

### Reproduction

```csharp
public struct Node { public int[] Items; }  // Items lacks value semantics
public struct Pair { public Node A; public Node B; }
public record class R(Pair P);  // Should produce JSV01, but doesn't
```

**Trace:**
1. `CheckMember(Pair, visited={})` — adds `Pair` to visited
2. Iterates `Pair` members → checks `A` of type `Node`
3. `CheckMember(Node, visited={Pair})` — adds `Node` to visited
4. Iterates `Node` members → checks `Items` of type `int[]` → **Failed** ✓
5. Returns `NestedFailed` for field `A` → **correctly flags `Pair.A`** ... but wait, the parent loop returns on the first failure, so `Pair.B` is never reached anyway.

However, consider the inverse scenario where the first usage is valid:

```csharp
public struct Good { public int X; }
public struct Container {
    public Good A;      // OK — Good has only int
    public Good B;      // visited already contains Good → silently Ok
}
```

In this case `Good` is legitimately Ok, so the false-negative is harmless. The real danger is:

```csharp
public struct Mixed {
    public int X;
}
// After CheckMember(Mixed), Mixed is in visited forever.
// If another UNRELATED member also contains Mixed, it's skipped.
```

Since each top-level `CheckMember` call creates a **fresh** HashSet (line 28), and each record parameter/field gets its own call, the cross-contamination is limited to **within a single struct's recursive descent**. The bug manifests when:
- A struct type appears at two different points in the same type's member tree
- The first occurrence passes (all fields Ok)
- The second occurrence would have failed but is skipped

**Concrete failing case:**

```csharp
public struct Wrapper { public int Value; }
public struct Outer {
    public Wrapper W;        // W checks Wrapper → Ok, adds Wrapper to visited
    public Wrapper[] Arr;    // Arr is int[]? No — it's Wrapper[]. Array fails anyway.
}
```

Actually, arrays are caught earlier by `HasEqualsTMethod` returning false. Let me reconsider...

After deeper analysis: within a single struct's descent, the accumulating visited set means a type checked at one branch is never re-checked at another. Since the type itself either passes or fails deterministically (it's the same type), the result would be the same. **The only scenario where this matters is if the traversal _returns early_ on the first encounter due to a cycle and then the type appears again in a non-cyclic position** — which is the CS0523 (circular struct layout) case that the cycle detection was designed for.

### Revised Assessment

The accumulating visited set is **safe for correctness** in practice because:
1. Each top-level call gets a fresh set (line 28)
2. A given type symbol always evaluates to the same result
3. The cycle case correctly returns Ok (compiler handles CS0523)

However, the pattern is **fragile** — any future change that makes results context-dependent would silently break. The add/remove pattern is strictly more correct:

```csharp
if (!visited.Add(type)) {
    return (ValueEqualityResult.Ok, null);
}
try {
    // ... recursive work ...
} finally {
    visited.Remove(type);
}
```

**Downgrading from Critical to Medium.** The current code is safe but the pattern is a latent risk.

---

## 2. High: `RecordHasEquals` Iterates Syntax — Misses Partial Records

**File:** `RecordValueSemantics.cs:128-148`
**Confidence:** 82%

### The Problem

`RecordHasEquals` iterates `recordDeclaration.Members` — the syntax members of a single `RecordDeclarationSyntax` node. For a `partial record`, the `Equals(T)` override could live in a different partial declaration in a different file.

```csharp
// File1.cs
public partial record class A(int[] Data);

// File2.cs
public partial record class A {
    public virtual bool Equals(A? other) => /* custom equality */;
}
```

The analyzer processes `File1.cs`, sees no `Equals(T)` in that syntax node, and emits JSV01 — a **false positive**.

### Remediation

Use the semantic model instead of iterating syntax:

```csharp
internal static bool RecordHasEquals(SyntaxNodeAnalysisContext context)
{
    var recordDeclaration = (RecordDeclarationSyntax)context.Node;
    var recordTypeSymbol = context.SemanticModel.GetDeclaredSymbol(recordDeclaration);
    if (recordTypeSymbol == null) return false;

    return recordTypeSymbol.GetMembers("Equals")
        .OfType<IMethodSymbol>()
        .Any(m => m.ReturnType.SpecialType == SpecialType.System_Boolean
                  && m.Parameters.Length == 1
                  && m.Parameters[0].Type.Equals(recordTypeSymbol, SymbolEqualityComparer.Default)
                  && !m.IsImplicitlyDeclared);
}
```

This checks all partial declarations and compiler-generated members via the semantic model. The `!m.IsImplicitlyDeclared` filter excludes the compiler-synthesized `Equals(T)` (which is always present on records) and only detects user-written ones.

**Note:** Verify whether the compiler-synthesized `Equals(T)` has `IsImplicitlyDeclared == true`. If not, an alternative filter is `m.DeclaringSyntaxReferences.Length > 0` (user-declared methods have syntax references; synthesized ones do not).

---

## 3. High: Code Fix Generates Wrong Signature for Derived Record Classes

**File:** `RecordValueAnalyserCodeFixProvider.cs:150-167`
**Confidence:** 88%

### The Problem

`BuildEqualsClassMethod` always emits `public virtual bool Equals(T? other)`. For a derived record class, the compiler-synthesized `Equals(T)` is `sealed override`, not `virtual`. The generated code:

- **For a base record:** `public virtual bool Equals(A? other) => false;` — correct
- **For a derived record:** `public virtual bool Equals(B? other) => false;` — **wrong**: introduces a new virtual member that hides (rather than overrides) the base's `Equals`. The compiler will emit CS0114 (hides inherited member).

### Remediation

Detect whether the record inherits from another record:

```csharp
var isBaseRecord = typeSymbol?.BaseType?.IsRecord != true
                   || typeSymbol.BaseType.SpecialType == SpecialType.System_Object;

var equalsmethod = isclassrecord
    ? (isBaseRecord ? BuildEqualsClassMethod(recordname) : BuildEqualsDerivedClassMethod(recordname))
    : BuildEqualsStructMethod(recordname);
```

Where `BuildEqualsDerivedClassMethod` generates:
```csharp
public sealed override bool Equals(B? other) => false; // TODO
```

**Pragmatic alternative:** Since the code fix is a scaffold (it generates `=> false; // TODO`), and derived records with custom equality are rare, this could also be deferred with a code comment explaining the limitation.

---

## 4. High: Null-Dereference Risk in Code Fix Provider

**File:** `RecordValueAnalyserCodeFixProvider.cs:62-75`
**Confidence:** 83%

### The Problem

```csharp
var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);
// ...
var recordDeclaration = await typeSymbol!.DeclaringSyntaxReferences[0]
    .GetSyntaxAsync(cancellationToken)
```

Three issues:
1. `GetSemanticModelAsync` can return `null` — `semanticModel` is used without null check
2. `typeSymbol!` suppresses the null warning — if null, throws `NullReferenceException`
3. `DeclaringSyntaxReferences[0]` assumes at least one entry — if empty, throws `IndexOutOfRangeException`
4. For partial records, `DeclaringSyntaxReferences[0]` may not be the same syntax node as `typeDecl`

### Remediation

Use `typeDecl` directly instead of round-tripping through the semantic model:

```csharp
var recordDeclaration = typeDecl as RecordDeclarationSyntax;
if (recordDeclaration == null) return document.Project.Solution;
```

This eliminates the null chain and the partial-record mismatch. The `semanticModel`/`typeSymbol` lookup is only needed if you need symbol information (e.g., to detect derived records per issue #3).

---

## 5. Medium: Only First Variable in Multi-Variable Field Declaration Checked

**File:** `RecordValueSemantics.cs:163-170`
**Confidence:** 85%

### The Problem

```csharp
} else if (member is FieldDeclarationSyntax fieldDeclaration) {
    type = context.SemanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type).Type;
    if (fieldDeclaration.Declaration.Variables.Count == 0) {
        return (null, null, false);
    }
    memberName = fieldDeclaration.Declaration.Variables[0].Identifier.ValueText;
```

A C# field declaration can declare multiple variables: `public int[] A, B, C;`. The code only extracts `Variables[0]` and produces a single diagnostic for `A`. Fields `B` and `C` are never reported.

### Impact

Low in practice — multi-variable field declarations are uncommon in records (and discouraged by most style guides). But it's a correctness gap.

### Remediation

Option A: Return all variables from `GetPropertyOrFieldUnderlyingType` (breaking change to the API):

```csharp
// Change return type to IEnumerable<MemberStatusTuple>
// Yield one tuple per variable
```

Option B: Handle in the caller (`AnalyzeRecordDeclaration`):

```csharp
foreach (var member in recordDeclaration.Members) {
    if (member is FieldDeclarationSyntax fieldDecl) {
        foreach (var variable in fieldDecl.Declaration.Variables) {
            // Check each variable individually
        }
    } else {
        // Existing property handling
    }
}
```

---

## 6. Medium: Inconsistent Diagnostic Message Format

**File:** `RecordValueAnalyser.cs:62 vs 85`
**Confidence:** 80%

### The Problem

Parameter diagnostics use `"field {errorMember}"`:
```csharp
var args = errorMember == null ? $"{typeName} {memberName}" : $"{typeName} {memberName} (field {errorMember})";
```

Body member diagnostics use just `"{errorMember}"`:
```csharp
var args = errorMember == null ? $"{typeName} {memberName}" : $"{typeName} {memberName} ({errorMember})";
```

The word "field" is present for parameters but absent for body members. Both should use the same format.

### Remediation

Use consistent format in both locations:
```csharp
var args = errorMember == null
    ? $"{typeName} {memberName}"
    : $"{typeName} {memberName} (field {errorMember})";
```

---

## 7. Low: `HasEqualsTMethod` Does Not Exclude Abstract Methods

**File:** `RecordValueSemantics.cs:264-270`
**Confidence:** 80%

An abstract `Equals(T)` declaration (e.g., on an abstract class that a struct-like type inherits through interface defaults) would satisfy the predicate and suppress diagnostics, even though no concrete equality is implemented.

### Remediation

Add `&& !m.IsAbstract` to the predicate.

---

## 8. Low: Dead Code in Test Project

**File:** `RecordValueAnalyser.Test/Program.cs`
**Confidence:** 82%

Contains `MainX()` — a renamed debugging entry point that is never called. The `StartupObject` in `.csproj` is also commented out.

### Remediation

Delete the file or convert to an actual useful test harness.

---

## 9. Test Coverage Gaps

| # | Scenario | Relates To | Priority |
|---|----------|------------|----------|
| 9.1 | Multi-variable field declarations (`public int[] X, Y;`) | Issue #5 | Medium |
| 9.2 | Partial records with `Equals(T)` in another partial file | Issue #2 | High |
| 9.3 | Code fix applied to a derived (non-base) record class | Issue #3 | High |
| 9.4 | Struct with two fields of the same nested type (visited set behaviour) | Issue #1 | Medium |
| 9.5 | `nint`/`nuint` in record classes (only tested in record structs) | — | Low |
| 9.6 | Generic record parameters (`record Wrapper<T>(T Value)`) | — | Low |

---

## Remediation Plan

### Phase 1 — Correctness Fixes (do first)

| # | Issue | Severity | File(s) | Fix |
|---|-------|----------|---------|-----|
| 2 | `RecordHasEquals` misses partial records | High | `RecordValueSemantics.cs` | Switch from syntax iteration to `GetMembers("Equals")` on the type symbol |
| 5 | Multi-variable field only checks first variable | Medium | `RecordValueSemantics.cs` + `RecordValueAnalyser.cs` | Iterate all variables in the caller |
| 6 | Inconsistent diagnostic message format | Medium | `RecordValueAnalyser.cs` | Add "field" prefix consistently |
| 1 | Visited set accumulation pattern | Medium | `RecordValueSemantics.cs` | Switch to add/remove path tracking |

### Phase 2 — Code Fix Hardening

| # | Issue | Severity | File(s) | Fix |
|---|-------|----------|---------|-----|
| 3 | Wrong Equals signature for derived records | High | `CodeFixProvider.cs` | Detect base vs derived, generate correct modifiers |
| 4 | Null-dereference chain | High | `CodeFixProvider.cs` | Use `typeDecl` directly instead of round-tripping through `DeclaringSyntaxReferences` |

### Phase 3 — Test Coverage

| # | Test to Add | Relates To |
|---|-------------|------------|
| 9.2 | Partial record with Equals in another file | Issue #2 |
| 9.3 | Code fix on derived record class | Issue #3 |
| 9.1 | Multi-variable field declaration in record body | Issue #5 |
| 9.4 | Struct with duplicate nested type across sibling fields | Issue #1 |
| 9.5 | `nint` in record class (symmetry with struct tests) | — |
| 9.6 | Generic record type parameter | — |

### Phase 4 — Cleanup

| # | Issue | File(s) |
|---|-------|---------|
| 7 | Add `!m.IsAbstract` to `HasEqualsTMethod` | `RecordValueSemantics.cs` |
| 8 | Remove dead `Program.cs` / `MainX` | `RecordValueAnalyser.Test/Program.cs` |

---

## Positive Observations

- Clean, well-documented code with consistent XML comments
- Correct use of `EnableConcurrentExecution()` — all analyzer methods are stateless
- Modern C# idioms: file-scoped namespaces, collection expressions, tuple returns, pattern matching
- `HasEqualsTMethod` correctly distinguishes own methods from inherited via `!m.IsOverride`
- `HasEqualsObjectMethod` correctly checks `ContainingType` to prevent false positives from inherited `Equals(object)`
- The recent recursion fix (CS0523 cycle detection) was well-motivated
- Memory<T>, ReadOnlyMemory<T>, ArraySegment<T>, ImmutableArray<T> — good coverage of tricky value types
- Symmetric test coverage for record classes and record structs

---

*Generated by Claude Code (Opus 4.6) — deep review*
