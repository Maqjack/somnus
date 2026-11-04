# 0001. Tech stack: C# on .NET 10 LTS with WPF
Date: 2026-10-22 · Status: Accepted

## Context
Somnus is a small Windows-only GUI app that calls Windows power APIs. SWPA section 4 covers Python and JS/TS; any other language must follow "Inne języki" and be recorded here. .NET 8 LTS (first proposed in chat) reaches end of support on 2026-11-10, three weeks after this decision, so it is not acceptable for a new project.

## Decision
- C# with .NET 10 (LTS, supported until November 2028), WPF for the UI, MVVM.
- Equivalents of SWPA section 4 tooling:
  - formatter/linter: `dotnet format` + .NET analyzers (`AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, `TreatWarningsAsErrors=true`), `.editorconfig`;
  - strict typing: `Nullable=enable` in all projects;
  - tests: xUnit + coverlet, line coverage ≥ 80% for `Somnus.Core` and `Somnus.Presentation`;
  - lock file: `RestorePackagesWithLockFile=true` (`packages.lock.json` committed), restore with `--locked-mode` in CI;
  - central package versions: `Directory.Packages.props`;
  - docs: XML doc comments on all public types and members (`GenerateDocumentationFile=true`, CS1591 as error).
- Rejected: Python + PyInstaller (heavy, frequent antivirus false positives, tray/power APIs via ctypes), Electron/Tauri (≈100 MB runtime for a timer, extra language), WinUI 3 (heavier packaging for unpackaged apps, smaller benefit here).

## Consequences
- Native look and direct P/Invoke to power APIs without extra libraries.
- Windows-only by design (matches scope).
- Logic projects target plain `net10.0` so they test on any OS; only adapters and UI target `net10.0-windows`.
