---
name: dotnet-pre-commit
description: Use before committing any changes in this project - runs format then full test suite
---

# .NET Pre-Commit

Run in order before every commit. Stop and fix on any failure — never skip with `--no-verify`.

```bash
dotnet build -c Debug RecordValueAnalyser.Test
dotnet format
gtimeout 120 dotnet test
```
