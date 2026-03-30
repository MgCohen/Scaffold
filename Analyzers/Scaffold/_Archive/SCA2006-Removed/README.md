# SCA2006 — archived (removed)

**Same-layer initialization usage** analysis (`InitializationSameLayerUsageAnalyzer`) is **not shipped**: the rule was removed in favor of handling initialization ordering outside static analysis (e.g. runtime dependency evaluation). Contract attributes under `Scaffold.Scope.Contracts` may remain in source for documentation or future use.

## Contents

| File | Notes |
| ---- | ----- |
| `InitializationSameLayerUsageAnalyzer.cs` | Former **SCA2006** `DiagnosticAnalyzer` (copy; not compiled). |
| `InitializationSameLayerUsageAnalyzerTests.cs` | Former tests (copy; not in test project). |
| `SCA2006-plan-archive.md` | Full prior plan text from `Plans/SCA-Analyzer-Revamp/SCA2006.md` before stub replacement. |

**Canonical status:** [Plans/SCA-Analyzer-Revamp/SCA2006.md](../../../Plans/SCA-Analyzer-Revamp/SCA2006.md)
