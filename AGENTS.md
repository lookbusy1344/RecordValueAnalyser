# Project Overview

RecordValueAnalyser is a C# Roslyn code analyzer that checks records for correct value semantics. The analyzer identifies when record members lack value semantics, which can cause equality comparisons to fail unexpectedly.

## Version control (read first)

Before the first VCS command, run `jj --ignore-working-copy root` as a command of its own. Do not chain it with other commands: a later failure hides its exit status. If it prints a path, the repo is jj. This may be a jj repo on one machine and plain Git on another.

**IMPORTANT:** if it succeeds, use `jj` for all VCS commands, including `log`, `show`, `status` and `diff`. Do not run `git` on jj repos. The `gitStatus` snapshot in the session context is not a reason to use git.

jj has no commit hooks. Run the pre-commit checks before `jj commit`, `jj describe` (when finalising a change) and `jj squash`.
If a change touches only non-code files (`*.md`), skip the dotnet steps.

`scripts/agent-pre-commit-hook.sh` runs `scripts/pre-commit.sh` on every `jj commit` and `jj squash` and blocks the command on failure. Claude Code and Codex call it as a `PreToolUse` hook. It does not gate `jj describe`; run the checks yourself when finalising. It gates `git commit` only when no Git pre-commit hook is installed.

Before `jj git push` or moving a shared bookmark, run `scripts/pre-push.sh`. By default it formats the tip (newest non-empty mutable revision) with `jj fix` (`scripts/format-stdin.sh`, whitespace rules), then runs restore, build, `dotnet format --verify-no-changes` and tests on it. `--full` formats every mutable revision in `::@` and checks each in its own checkout via `jj run`. `scripts/agent-pre-push-hook.sh` runs the tip check on every push command and blocks the push on failure. Claude Code (`.claude/settings.json`) and Codex (`.codex/hooks.json`) call it as a `PreToolUse` hook. The hook checks the tip of `@`, not the bookmark being pushed, so push only the bookmark you are working on: the one on `@-`, with an empty `@` above it. In a plain Git checkout it runs the same checks on the working tree without formatting it, and refuses uncommitted changes to tracked files so the checked tree is the pushed HEAD. `git push` triggers the same hook.

Push only on explicit request. "Push this" means: if `@` is non-empty, `jj commit` it (after the pre-commit checks). Move the bookmark to `@-` with `jj bookmark set <name> -r @-`, then `jj git push --bookmark <name>`. Use the bookmark already on the stack; otherwise `main`. Never push any other bookmark. Do not use `jj git push -c`.

## Personal information

Exclude PII from every commit, commit message and bookmark name: real names, email addresses, usernames, machine paths such as `/Users/<name>/`, hostnames, tokens and credentials. Check the diff before `jj commit`, `jj describe` (finalising) and `git commit`.

## Solution Architecture

5 projects: core analyzer (`RecordValueAnalyser`), code fixes (`RecordValueAnalyser.CodeFixes`), NuGet package wrapper, MSTest tests (`RecordValueAnalyser.Test`, net10.0), and VS extension (`RecordValueAnalyser.Vsix`).

Legacy builds for .NET 8/9 SDKs and Visual Studio 2022: `RecordValueAnalyser.Roslyn48` and `RecordValueAnalyser.CodeFixes.Roslyn48` compile the same sources against Roslyn 4.8 with `LEGACY_ROSLYN` defined. Roslyn 4.8 has no `ITypeSymbol.IsUnion`, so that build reads it by reflection from the running compiler and sees no union types under compilers without it. `RecordValueAnalyser.Test.Roslyn48` runs the class and struct tests against them on Roslyn 4.8. `RecordValueAnalyser.Test.Roslyn48OnRoslyn59` runs every test against them on Roslyn 5.9, as the VSIX runs in Visual Studio 2026. The package ships both builds, in `analyzers/dotnet/roslyn4.8/cs` and `analyzers/dotnet/roslyn5.9/cs`. The VSIX ships only the Roslyn 4.8 build, since Visual Studio cannot pick a VSIX analyzer build by compiler version, and finds union types through the running compiler. The VSIX builds only on Windows. `scripts/check-package.sh <package-dir> <sdk-major> <tfm> <roslyn-folder>` builds a consumer project against a packed package with one SDK. It checks which folder the SDK loads and that JSV01 is reported exactly once. CI runs it for .NET 8, 9, 10 and 11 in the `package-consumer` job.

Shared sources must compile against Roslyn 4.8 and System.Collections.Immutable 7.0. Do not use collection expressions for `ImmutableArray<T>` or `SyntaxList<T>`: for `SyntaxList<T>` the 4.8 build compiles them as a collection initializer whose `Add` results are discarded, which yields an empty list.

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

## Running workflows locally

`act push -W .github/workflows/test.yml` runs the CI workflow in Docker, with flags from `.actrc` (see README). Use it once to check a workflow change, not while iterating: a full run takes several minutes.

- Run `act` with `dangerouslyDisableSandbox: true`. It uses the Docker socket, and its artifact server listens for the job containers.
- If a run is interrupted, remove the leftover containers: `docker ps -a --filter name=act-`, then `docker rm -f` each one.

## Pre-Commit

Run in order before every commit. Stop and fix on any failure — never skip with `--no-verify`.

```bash
dotnet build -c Debug RecordValueAnalyser.Test
dotnet format
gtimeout 120 dotnet test
```

- **IMPORTANT** Every `dotnet` Bash call must set `dangerouslyDisableSandbox: true` (build, test, format, run, restore, publish, and any `gtimeout`-wrapped variants). The sandbox blocks `dotnet` even when listed in `excludedCommands`: MSBuild's Unix-domain sockets for diagnostic IPC and worker-node communication fail under `network-inbound` deny, and the EPERM surfaces as a silent generic build failure.
- **IMPORTANT** `git commit` in this repo runs `.git/hooks/pre-commit` → `scripts/pre-commit.sh`, which itself invokes `dotnet build`, `dotnet format --verify-no-changes`, and `dotnet test`. So `git commit` must ALSO be run with `dangerouslyDisableSandbox: true` — otherwise the hook's nested `dotnet` calls hang/silently fail and the commit appears stuck with no error output.
