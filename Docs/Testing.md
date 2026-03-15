# Testing

## Purpose

This document explains how to run automated checks in this repository and how the PowerShell validation scripts behave.

The primary quality gate is `.agents/scripts/validate-changes.cmd` (wrapper that runs PowerShell with `-ExecutionPolicy Bypass`).

## Recommended Workflow

From the repository root, run:

```powershell
& ".\.agents\scripts\validate-changes.cmd"
```

The validation pipeline runs in this order:

1. `.agents/scripts/check-unity-compilation.ps1`
2. `.agents/scripts/run-editmode-tests.ps1` (only if compilation precheck passes)
3. `.agents/scripts/check-analyzers.ps1`

This order catches direct Unity compile blockers before attempting EditMode tests.

## Script Roles

### Unity Compilation Precheck

Run manually when you want fast compile validation without tests:

```powershell
& ".\.agents\scripts\check-unity-compilation.ps1"
```

This script:

- resolves Unity from `-UnityPath`, then `UNITY_PATH`, then `ProjectVersion.txt` + Unity Hub paths
- runs Unity batchmode compile precheck
- reports compiler blockers (for example, `error CSxxxx`)
- times out and fails cleanly if Unity does not exit in time

### Headless EditMode Tests

Run manually when you want direct test execution:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1"
```

This script:

- runs Unity EditMode tests in batchmode
- prints total/passed/failed/skipped counts
- prints failed test names/messages
- times out and fails cleanly if Unity does not exit in time
- deletes temporary artifacts before exit

### Analyzer Diagnostics

Run analyzer checks directly:

```powershell
powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"
```

Output format:

- `TOTAL:<n>`
- `RULE:<code>:<count>`
- `FILE:<relative-path>:<count>`
- `DIAG:<raw SCA diagnostic line>`
- `BLOCKER:<raw line>`

If no `.sln` exists at the repository root, analyzer check is skipped with `TOTAL:0` and a `NOTE:` line.

When analyzer checks fail (`TOTAL > 0` or blockers exist), `validate-changes.ps1` appends an agent-readable contract:

- `AGENT_TASK_BEGIN` / `AGENT_TASK_END`
- `AGENT_ANALYZER_DIAGNOSTICS_BEGIN` / `AGENT_ANALYZER_DIAGNOSTICS_END`

Inside that diagnostics block, blockers are printed first (`BLOCKER:` lines), followed by all deduplicated raw SCA diagnostics (verbatim).

## Parameters

### check-unity-compilation.ps1

- `-ProjectPath`
- `-UnityPath`
- `-TimeoutMinutes` (default `10`)

### run-editmode-tests.ps1

- `-ProjectPath`
- `-UnityPath`
- `-AssemblyNames`
- `-TimeoutMinutes` (default `30`)

### check-analyzers.ps1

- `-ProjectPath`
- `-TimeoutMinutes` (default `10`)

### validate-changes.ps1

- `-ProjectPath`
- `-UnityPath`
- `-AssemblyNames`
- `-CompilationTimeoutMinutes` (default `10`)
- `-TestTimeoutMinutes` (default `30`)
- `-AnalyzerTimeoutMinutes` (default `10`)

## validate-changes Exit Codes

- `0`: compilation precheck passed, tests passed, analyzer checks clean
- `1`: compilation or tests failed/blocked
- `2`: analyzer diagnostics/blockers remain
- `3`: test gate and analyzer gate both failed

## Legacy Milestone Script

`.agents/scripts/validate-milestone.ps1` is still available and can be used for older milestone workflows. Prefer `validate-changes` for the current gate because it includes the compilation precheck stage and richer analyzer-contract output.

## Troubleshooting

### Compiler errors block tests

If Unity reports `Scripts have compiler errors`, fix compile errors first. EditMode tests cannot run until compilation succeeds.

### Another Unity instance is running

If Unity logs indicate the project is already open elsewhere, close the other Unity process and rerun.

### Validation appears stuck

All pipeline stages have timeouts. If a stage exceeds its timeout, the script exits that stage with a clear blocker message.

## Related Files

- `.agents/scripts/check-unity-compilation.ps1`
- `.agents/scripts/run-editmode-tests.ps1`
- `.agents/scripts/check-analyzers.ps1`
- `.agents/scripts/validate-changes.cmd`
- `.agents/scripts/validate-changes.ps1`
- `.agents/scripts/validate-milestone.ps1`
- `Architecture.md`
- `AGENTS.md`
