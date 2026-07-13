# Project Overview

RecordValueAnalyser is a C# Roslyn code analyzer that checks records for correct value semantics. The analyzer identifies when record members lack value semantics, which can cause equality comparisons to fail unexpectedly.

## Solution Architecture

5 projects: core analyzer (`RecordValueAnalyser`), code fixes (`RecordValueAnalyser.CodeFixes`), NuGet package wrapper, MSTest tests (`RecordValueAnalyser.Test`, net10.0), and VS extension (`RecordValueAnalyser.Vsix`).

Main logic split between `RecordValueAnalyser.cs` (entry point / diagnostics) and `RecordValueSemantics.cs` (value semantics checks).

## Key Implementation Details

### Analyzer Logic
1. Skip records with custom `Equals(T)` methods
2. Check record parameters, fields, and properties for value semantics
3. Recursively analyze nested structs and tuples
4. Report **JSV01** diagnostic for members lacking value semantics

### Target Framework
- Analyzer: .NET Standard 2.0
- Tests: .NET 10.0, C# 12-14, nullable reference types enabled

## Pre-Commit

Run in order before every commit. Stop and fix on any failure — never skip with `--no-verify`.

```bash
dotnet build -c Debug RecordValueAnalyser.Test
dotnet format
gtimeout 120 dotnet test
```

- **IMPORTANT** Every `dotnet` Bash call must set `dangerouslyDisableSandbox: true` (build, test, format, run, restore, publish, and any `gtimeout`-wrapped variants). The sandbox blocks `dotnet` even when listed in `excludedCommands`: MSBuild's Unix-domain sockets for diagnostic IPC and worker-node communication fail under `network-inbound` deny, and the EPERM surfaces as a silent generic build failure.
- **IMPORTANT** `git commit` in this repo runs `.git/hooks/pre-commit` → `scripts/pre-commit.sh`, which itself invokes `dotnet build`, `dotnet format --verify-no-changes`, and `dotnet test`. So `git commit` must ALSO be run with `dangerouslyDisableSandbox: true` — otherwise the hook's nested `dotnet` calls hang/silently fail and the commit appears stuck with no error output.
