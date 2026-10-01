# Project Overview

RecordValueAnalyser is a C# Roslyn code analyzer that checks records for correct value semantics. The analyzer identifies when record members lack value semantics, which can cause equality comparisons to fail unexpectedly.

## Version control (read first)

Before the first VCS command, run `jj --ignore-working-copy root`. This may be a jj repo on one machine and plain Git on another.

**IMPORTANT:** if it succeeds, use `jj` for all VCS commands, including `log`, `show`, `status` and `diff`. Do not run `git` on jj repos. The `gitStatus` snapshot in the session context is not a reason to use git.

jj has no commit hooks. Run the pre-commit checks before `jj commit`, `jj describe` (when finalising a change) and `jj squash`.
If a change touches only non-code files (`*.md`), skip the dotnet steps.

Before `jj git push` or moving a shared bookmark, run `scripts/pre-push.sh`. By default it formats the tip (newest non-empty mutable revision) with `jj fix` (`scripts/format-stdin.sh`, whitespace rules), then runs restore, build, `dotnet format --verify-no-changes` and tests on it. `--full` formats every mutable revision in `::@` and checks each in its own checkout via `jj run`. `scripts/agent-pre-push-hook.sh` runs the tip check on every push command and blocks the push on failure. Claude Code (`.claude/settings.json`) and Codex (`.codex/hooks.json`) call it as a `PreToolUse` hook. The hook checks the tip of `@`, not the bookmark being pushed, so push only the bookmark you are working on: the one on `@-`, with an empty `@` above it. In a plain Git checkout it formats the working tree with `dotnet format` and runs the same checks, and `git push` triggers the same hook.

Push only on explicit request. "Push this" means: if `@` is non-empty, `jj commit` it (after the pre-commit checks). Move the bookmark to `@-` with `jj bookmark set <name> -r @-`, then `jj git push --bookmark <name>`. Use the bookmark already on the stack; otherwise `main`. Never push any other bookmark. Do not use `jj git push -c`.

## Personal information

Exclude PII from every commit, commit message and bookmark name: real names, email addresses, usernames, machine paths such as `/Users/<name>/`, hostnames, tokens and credentials. Check the diff before `jj commit`, `jj describe` (finalising) and `git commit`.

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
