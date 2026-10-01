---
name: require-pre-commit-checks
enabled: true
event: bash
pattern: (jj\s+(commit|describe|squash)|git\s+commit)
action: warn
---

**Pre-commit check required.**

Inspect the changed files: `jj diff --name-only` in a jj repo (`jj --ignore-working-copy root` succeeds), otherwise `git diff HEAD --name-only`.

**If all changed files are documentation only** (`.md` files, `docs/`, `README`):
- No build or test checks required. Proceed.

**If any .NET source files changed** (`.cs`, `.csproj`, `.sln`, `.editorconfig`, `.props`, `.targets`):
- Run `scripts/pre-commit.sh`, or all of the following in order:

```bash
dotnet build --configuration Debug --no-restore RecordValueAnalyser.Test
dotnet format RecordValueAnalyser.sln --verify-no-changes
gtimeout 120 dotnet test --no-restore
```

All steps must pass with zero errors. Stop and fix on any failure.

In a jj repo, `jj describe` on an unfinished change does not need the checks; run them when finalising.

If you have already completed all checks in this session and they passed, you may proceed.
