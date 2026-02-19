# Code Review: RecordValueAnalyser

**Date:** 2026-02-19
**Reviewer:** Claude Code (Opus 4.6)
**Commit:** `6a32676` (main)
**Test status:** All 59 tests passing

---

## Executive Summary

RecordValueAnalyser is a well-structured Roslyn analyzer with solid test coverage. The core analysis logic in `RecordValueSemantics.cs` is sound and handles the key cases correctly. However, the review identified **1 bug**, **3 coverage gaps**, **3 test quality issues**, and **3 maintenance concerns** worth addressing.

Severity ratings: **Critical** / **High** / **Medium** / **Low**

---

## 1. Bugs

### 1.1 [High] Missing `CancellationToken` in code fix provider

**File:** `RecordValueAnalyser.CodeFixes/RecordValueAnalyserCodeFixProvider.cs:95`

```csharp
var oldRoot = await document.GetSyntaxRootAsync().ConfigureAwait(false);
```

The `cancellationToken` parameter is available in the method signature but not passed to `GetSyntaxRootAsync()`. The same call on line 28 correctly passes `context.CancellationToken`. This means the code fix operation cannot be cancelled by the user once it reaches this point, and in pathological cases could hang the IDE.

**Remediation:** Change to:
```csharp
var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
```

---

## 2. Analyzer Coverage Gaps

### 2.1 [Medium] `Memory<T>` and `ReadOnlyMemory<T>` not flagged

**File:** `RecordValueAnalyser/RecordValueSemantics.cs`

`Memory<T>` and `ReadOnlyMemory<T>` are value types (structs) that wrap array references. Their default equality compares the underlying array reference, not contents. They are analogous to `ArraySegment<T>` which _is_ explicitly flagged. `Span<T>` is a ref struct and cannot appear in records, so it's not relevant.

**Remediation:** Add checks similar to `IsArraySegmentType`:
```csharp
private static bool IsMemoryType(ITypeSymbol? typeSymbol) =>
    GetGenericName(typeSymbol) is "System.Memory<T>" or "System.ReadOnlyMemory<T>";
```
Add a corresponding check block in `CheckMember` between the `ArraySegment` check and the tuple check. Add tests for both types.

---

### 2.2 [Low] No handling of generic record type parameters

**File:** `RecordValueAnalyser/RecordValueSemantics.cs`

Records with unconstrained generic type parameters are not tested:
```csharp
public record Wrapper<T>(T Value);
```

When `T` is unconstrained, `Value` should be flagged since `T` could be any type. Currently, this falls through to the default `Failed` return at line 108, which is correct. But it would be good to have explicit tests confirming this behaviour, and to handle constrained generics (`where T : struct`, `where T : IEquatable<T>`) more precisely.

**Remediation:** Add tests for:
- `record Wrapper<T>(T Value)` - should fail
- `record Wrapper<T>(T Value) where T : struct` - debatable, could pass
- `record Wrapper<T>(T Value) where T : IEquatable<T>` - debatable, could pass

---

### 2.3 [Low] Unused resource strings

**File:** `RecordValueAnalyser/RecordValueAnalyser.cs:14-16` vs `RecordValueAnalyser/Resources.Designer.cs`

The `DiagnosticDescriptor` at lines 14-16 hardcodes its title, message format, and description strings rather than using the localizable strings from `Resources.resx` (`AnalyzerTitle`, `AnalyzerMessageFormat`, `AnalyzerDescription`). The resource infrastructure exists and the `.resx` file is embedded, but it's entirely bypassed.

**Remediation:** Either use the resource strings (recommended for localization) or remove the `.resx` infrastructure to avoid confusion:
```csharp
private static readonly DiagnosticDescriptor ParamValueSemanticsRule = new(
    DiagnosticId,
    new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources)),
    new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources)),
    "Design",
    DiagnosticSeverity.Warning,
    true,
    new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources)));
```

---

## 3. Test Quality Issues

### 3.1 [Medium] Misleading test names: `NestedClassEqualsPass` and `NestedClassInvalidEqualsFail`

**Files:** `RecordValueAnalyser.Test/UnitTestsStructs.cs:106-139`, `RecordValueAnalyser.Test/UnitTestsClasses.cs:106-139`

These tests declare a type named `ClassA` but it is actually a `struct`:
```csharp
public struct ClassA { public int I; public int[] Numbers;
    public bool Equals(ClassA other) => false;
}
```

The test names suggest class behaviour is being tested, but the type is a struct. This is confusing and could mask a gap in test coverage for actual nested class types with equals.

**Remediation:** Rename `ClassA` to `StructB` or similar, and add separate tests for actual nested classes with `Equals` overrides.

---

### 3.2 [Medium] Duplicated test infrastructure

**Files:** `UnitTestsStructs.cs:15-33`, `UnitTestsClasses.cs:15-33`

The `coGeneral` and `coInlineArrayAttribute` constants are identically defined in both test files.

**Remediation:** Extract to a shared `TestConstants` class:
```csharp
internal static class TestConstants
{
    internal const string General = @"...";
    internal const string InlineArrayAttribute = @"...";
}
```

---

### 3.3 [Low] Missing test scenarios

The test suite is comprehensive for the existing feature set but lacks coverage for:

| Scenario | Status |
|----------|--------|
| Generic record parameters (`record Wrapper<T>(T Value)`) | Missing |
| Record class inheritance with body properties | Missing |
| `Memory<T>` / `ReadOnlyMemory<T>` members | Missing (type not handled) |
| Nullable reference type members (`string?` vs `string`) | Missing |
| Record with multiple failing members (body + parameter mix) | Missing |
| Deeply nested struct chains (3+ levels) | Missing |

**Remediation:** Add tests incrementally as new type checks are added. Prioritize generic record tests and mixed body/parameter failure tests.

---

## 4. Maintenance Concerns

### 4.1 [Medium] Unnecessary NuGet packages in test project

**File:** `RecordValueAnalyser.Test/RecordValueAnalyser.Test.csproj`

The build warns about three unnecessary packages:
```
NU1510: PackageReference System.Formats.Asn1 will not be pruned
NU1510: PackageReference System.Net.Http will not be pruned
NU1510: PackageReference System.Text.RegularExpressions will not be pruned
```

These appear to be transitive dependency overrides that are no longer needed on .NET 10.

**Remediation:** Remove these three `<PackageReference>` entries:
```xml
<!-- Remove these -->
<PackageReference Include="System.Formats.Asn1" Version="10.0.3" />
<PackageReference Include="System.Net.Http" Version="4.3.4" />
<PackageReference Include="System.Text.RegularExpressions" Version="4.3.1" />
```

---

### 4.2 [Medium] Version hardcoded in three `.csproj` files

**Files:**
- `RecordValueAnalyser/RecordValueAnalyser.csproj:10` - `1.3.0.0`
- `RecordValueAnalyser.Package/RecordValueAnalyser.Package.csproj:10-11` - `1.3.0.0` (twice)
- `RecordValueAnalyser.Vsix/RecordValueAnalyser.Vsix.csproj:41` - `1.3.0.0`

**Remediation:** Centralise the version in a `Directory.Build.props` file:
```xml
<Project>
  <PropertyGroup>
    <Version>1.3.0.0</Version>
  </PropertyGroup>
</Project>
```

---

### 4.3 [Low] Struct member double-evaluation

**File:** `RecordValueAnalyser/RecordValueSemantics.cs:79`

When analysing struct members, `type.GetMembers()` returns all members including auto-property backing fields. For a struct with `public int X { get; set; }`, the code checks both the `X` property symbol and the compiler-generated `<X>k__BackingField` field symbol. Since both resolve to `int`, the result is correct but redundant.

**Remediation:** Filter to only user-declared members, or filter out compiler-generated backing fields:
```csharp
members = type.GetMembers()
    .Where(m => m is IFieldSymbol f ? !f.IsImplicitlyDeclared : m is IPropertySymbol);
```

---

## 5. Remediation Plan

### Priority 1 - Bug (fix immediately)

| # | Issue | File | Effort |
|---|-------|------|--------|
| 1.1 | Pass `cancellationToken` to `GetSyntaxRootAsync` | `CodeFixProvider.cs:95` | 1 min |

### Priority 2 - Coverage Gaps (next release)

| # | Issue | Files | Effort |
|---|-------|-------|--------|
| 2.1 | Add `Memory<T>` / `ReadOnlyMemory<T>` detection | `RecordValueSemantics.cs` + tests | 30 min |
| 2.3 | Use resource strings or remove `.resx` | `RecordValueAnalyser.cs` | 15 min |
| 3.3 | Add missing test scenarios (generics, mixed members, deep nesting) | Test files | 1-2 hr |

### Priority 3 - Quality / Maintenance (when convenient)

| # | Issue | Files | Effort |
|---|-------|-------|--------|
| 3.1 | Rename misleading `ClassA` in tests | Both test files | 10 min |
| 3.2 | Extract shared test constants | Both test files | 10 min |
| 4.1 | Remove unnecessary NuGet packages | `Test.csproj` | 2 min |
| 4.2 | Centralise version in `Directory.Build.props` | 3 `.csproj` files | 15 min |
| 4.3 | Filter compiler-generated backing fields | `RecordValueSemantics.cs:79` | 10 min |

### Total estimated effort: ~3 hours

---

## 6. Positive Observations

- The core analyzer logic is clean, well-documented with XML comments, and follows a clear decision tree
- Concurrent execution is enabled (`EnableConcurrentExecution`), and the analyzer methods are stateless, which is correct
- Good use of modern C# features: file-scoped namespaces, collection expressions, tuple return types, pattern matching
- The `HasEqualsTMethod` check correctly distinguishes own methods from inherited ones via `!m.IsOverride`
- The `HasEqualsObjectMethod` check correctly verifies the method is an override defined in the containing type, preventing false positives from inherited `Equals(object)`
- Thorough `.editorconfig` configuration with consistent style enforcement
- The code fix provider correctly handles both semicolon-terminated and brace-terminated records
- Test suite covers both record classes and record structs symmetrically, which is good

---

*Generated by Claude Code (Opus 4.6)*
