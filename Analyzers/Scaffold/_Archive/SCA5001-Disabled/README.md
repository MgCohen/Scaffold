# SCA5001 — archived (disabled)

**Invariant entry-point validation** for public runtime methods is **not shipped**: the analyzer is removed from `Scaffold.Analyzers` until a **replan / refactor** (scope, heuristics, `InvariantUsageScope` alignment).

## Contents

| File | Notes |
| ---- | ----- |
| `InvariantEntryPointAnalyzer.cs` | Former **SCA5001** `DiagnosticAnalyzer` (copy; not compiled). |
| `InvariantEntryPointAnalyzerTests.cs` | Former tests (copy; not in test project). |
| `SCA5001-plan-archive.md` | Full prior plan text from `Plans/SCA-Analyzer-Revamp/SCA5001.md` before stub replacement. |

**Canonical status:** [Plans/SCA-Analyzer-Revamp/SCA5001.md](../../../Plans/SCA-Analyzer-Revamp/SCA5001.md)

**Shared helper archived with SCA5002:** `InvariantUsageScope.cs` is under [`_Archive/SCA5002-Disabled`](../SCA5002-Disabled/README.md) (constructor rule disabled; same replan as **SCA5001**).
