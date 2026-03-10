---
description: run Scaffold analyzer diagnostics across the solution, report by rule, fix iteratively, and verify zero remaining diagnostics
---

When invoked with `/fix-analyzers`, execute the following steps autonomously. Do not stop for user input unless a fix requires an architectural decision outside the diagnostic scope.

> Note: The analyzer DLL is assumed to already be built. Rebuild it only when analyzer source files changed (via `/create-custom-analyzer` or `dotnet build -c Release` in the analyzer project directory).

---

## Step 1 - Run the Script

Use the PowerShell script in this Windows workspace:

```powershell
powershell -ExecutionPolicy Bypass -File "C:/Users/user/Documents/Unity/Scaffold/.agents/scripts/check-analyzers.ps1"
```

The script builds with `--no-incremental` and emits deduplicated parseable lines:
- `TOTAL:<n>`
- `RULE:<code>:<count>`
- `FILE:<relative-path>:<count>`
- `BLOCKER:<raw error line>`

---

## Step 2 - Parse and Report Diagnostics

From the output, extract `RULE:` and `FILE:` lines.

Do not count `BLOCKER:` lines in analyzer totals. Treat blockers as build blockers and fix them first.

Compute:
- Total SCA diagnostic count (`TOTAL:`)
- Per-rule counts (`RULE:`)

Report this summary before fixing:

```text
Analyzer Diagnostics Report
----------------------------
Total: X diagnostics

SCA0003: N - Nested call/object construction
SCA0005: N - Line break inside statement
SCA0006: N - Method too long
...
```

---

## Step 3 - Fix All Diagnostics

Work through diagnostics file by file:

1. Open the file from each diagnostic path.
2. Navigate to the reported line and column.
3. Apply the fix described by the diagnostic message.
4. Save and continue.

Group fixes by file to avoid redundant reads.

---

## Step 4 - Verify

Re-run Step 1 and compare `TOTAL:` to the prior value.

- If SCA diagnostics remain, repeat Step 3 for the remaining diagnostics.
- Continue until `TOTAL:0`.
- If fixes introduce new diagnostics elsewhere, fix those too.

---

## Step 5 - Cleanup

Delete `msbuild.binlog` in the project root if present:

```powershell
Remove-Item -Force "C:/Users/user/Documents/Unity/Scaffold/msbuild.binlog" -ErrorAction SilentlyContinue
```

---

## Step 6 - Final Report

Report:

```text
Fix-analyzers complete.
Fixed: X diagnostics across Y files.
Rules cleared: SCA0003, SCA0005, SCA0006, ...
Remaining: 0
```

If anything could not be fixed, list each remaining diagnostic and the reason.
