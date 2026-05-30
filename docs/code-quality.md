# Code Quality Tooling

Hermes Desktop uses .NET analyzers plus Roslynator for local cleanup passes.

## Installed Tools

- `dotnet format` for whitespace, style, and analyzer cleanup.
- `roslynator.dotnet.cli` as a local .NET tool.
- `Roslynator.Analyzers`, `Meziantou.Analyzer`, and `SonarAnalyzer.CSharp` through `Directory.Build.props`.

The first analyzer adoption is intentionally non-disruptive: analyzer diagnostics default to suggestions in `.editorconfig`, while compiler warnings still build as warnings.

## Commands

Restore tools:

```powershell
dotnet tool restore
```

Run the full quality check:

```powershell
.\scripts\code-quality.ps1
```

The default check restores tools, restores packages, runs Roslynator at error
severity per project, builds, and runs tests. It uses project files because the
current Roslynator CLI can trip over this repo's .NET 10 solution parsing.

Run a stricter warning-level Roslynator pass:

```powershell
.\scripts\code-quality.ps1 -Strict
```

Apply safe formatting and Roslynator fixes:

```powershell
.\scripts\code-quality.ps1 -Fix
```

Run just the built-in formatter check when you are ready to normalize existing
whitespace:

```powershell
dotnet format HermesDesktop.sln --verify-no-changes
```

Run Roslynator analysis directly:

```powershell
dotnet roslynator analyze HermesDesktop.sln
```
