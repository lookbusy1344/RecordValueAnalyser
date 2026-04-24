---
name: require-pre-commit-checks
enabled: true
event: bash
pattern: git\s+commit
action: warn
---

**Pre-commit check required.**

Before proceeding, run `git diff --cached --name-only` to inspect staged files.

**If all staged files are documentation only** (`.md` files, `docs/`, `README`):
- No build or test checks required. Proceed with the commit.

**If any .NET source files are staged** (`.cs`, `.csproj`, `.sln`, `.editorconfig`, `.props`, `.targets`):
- You must run ALL of the following in order before committing:

```bash
dotnet build -c Debug RecordValueAnalyser.Test
dotnet format
gtimeout 120 dotnet test
```

All steps must pass with zero errors. Stop and fix on any failure — never skip with `--no-verify`.

If you have already completed all checks in this session and they passed, you may proceed.
