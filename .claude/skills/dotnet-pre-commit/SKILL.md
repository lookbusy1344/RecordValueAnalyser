---
name: dotnet-pre-commit
description: Use before git commit, jj commit, jj describe (finalising) or jj squash in this project - runs build, format check and tests
---

# .NET Pre-Commit

Run before `git commit`, or in a jj repo (no commit hooks) before `jj commit`, `jj describe` (finalising) and `jj squash`. Stop and fix on any failure.

```bash
scripts/pre-commit.sh
```

It runs the steps below and skips them for documentation-only changes:

```bash
dotnet build --configuration Debug --no-restore RecordValueAnalyser.Test
dotnet format RecordValueAnalyser.sln --verify-no-changes
gtimeout 120 dotnet test --no-restore
```

Before a push, run `scripts/pre-push.sh` (see AGENTS.md).
