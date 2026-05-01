# SCA5002 — archived (disabled)

**Public runtime constructor parameter validation** (`ConstructorInvariantAnalyzer`) is **not shipped**: the analyzer is removed from `Scaffold.Analyzers` until a **replan / refactor** together with **SCA5001** (method entry guards). **`InvariantUsageScope`** (asmdef external-consumption heuristic) is archived here as well; it was shared with the former **SCA5001** `InvariantEntryPointAnalyzer` copy under [`_Archive/SCA5001-Disabled`](../SCA5001-Disabled/README.md).

## Contents

| File | Notes |
| ---- | ----- |
| `ConstructorInvariantAnalyzer.cs` | Former **SCA5002** `DiagnosticAnalyzer` (copy; not compiled). |
| `InvariantUsageScope.cs` | Shared usage-scope helper (formerly compiled in main project). |
| `ConstructorInvariantAnalyzerTests.cs` | Former tests (copy; not in test project). |
| `SCA5002-plan-archive.md` | Full prior plan text from `Plans/SCA-Analyzer-Revamp/SCA5002.md` before stub replacement. |

**Canonical status:** [Plans/SCA-Analyzer-Revamp/SCA5002.md](../../../Plans/SCA-Analyzer-Revamp/SCA5002.md)

**Related:** [SCA5001.md](../../../Plans/SCA-Analyzer-Revamp/SCA5001.md) · [`_Archive/SCA5001-Disabled`](../SCA5001-Disabled/README.md)
