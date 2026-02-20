# Project Overview

RecordValueAnalyser is a C# Roslyn code analyzer that checks records for correct value semantics. The analyzer identifies when record members lack value semantics, which can cause equality comparisons to fail unexpectedly.

## Solution Architecture

The solution consists of 5 projects:

- **RecordValueAnalyser**: Core analyzer implementation (netstandard2.0)
- **RecordValueAnalyser.CodeFixes**: Code fix provider for the analyzer
- **RecordValueAnalyser.Package**: NuGet package wrapper
- **RecordValueAnalyser.Test**: MSTest-based unit tests (net10.0)
- **RecordValueAnalyser.Vsix**: Visual Studio extension

The main analyzer logic is split between:
- `RecordValueAnalyser.cs`: Main analyzer entry point and diagnostic reporting
- `RecordValueSemantics.cs`: Core logic for checking value semantics

## Development Commands

### Build
```bash
dotnet build                              # Build all projects
dotnet build -c Release                   # Release build
dotnet clean                              # Clean solution
```

### Test
```bash
dotnet build -c Debug RecordValueAnalyser.Test
dotnet test
```

### Package
```bash
dotnet build -c Release RecordValueAnalyser.Package
```

### Code Formatting
```bash
dotnet format                             # Format code after making changes
```

## Testing Framework

Uses MSTest with Roslyn analyzer testing framework:
- Tests are in `RecordValueAnalyser.Test` project
- Uses `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.MSTest` for analyzer testing
- Tests verify both diagnostics and code fixes

## Key Implementation Details

### Analyzer Logic
The analyzer performs these checks:
1. Skip records that have custom `Equals(T)` methods
2. Check record parameters, fields, and properties for value semantics
3. Recursively analyze nested structs and tuples
4. Report JSV01 diagnostic for members lacking value semantics

### Diagnostic ID
- **JSV01**: Member lacks value semantics

### Target Framework
- Analyzer: .NET Standard 2.0 (for broad compatibility)
- Tests: .NET 10.0
- Uses C# 12-14 language features with nullable reference types enabled

### Code Style
- Uses extensive static analysis rules (Roslynator, Visual Studio analyzers)
- Enforces code style in build with `EnforceCodeStyleInBuild`
- Multiple `.editorconfig` files for consistent formatting across projects
- LF line endings for CS files
- **Important**: Always run `dotnet format` after making code changes
