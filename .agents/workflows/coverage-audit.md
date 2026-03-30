---
description: Runs an explicit coverage audit and generates coverage reports.
---

1. Run from repository root:
   - `.agents/scripts/run-coverage-audit.cmd`
2. Optional targeted audit:
   - `.agents/scripts/run-coverage-audit.ps1 -AssemblyNames "<Module.Tests>"`
3. Optional custom output path:
   - `.agents/scripts/run-coverage-audit.ps1 -CoverageResultsPath "<path>"`
4. Review report output under `Coverage/Report/` (or provided custom path).
5. Do not treat coverage as a quality gate by itself; use it during explicit test audits.
