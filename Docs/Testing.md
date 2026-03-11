# Testing

## Purpose

This document explains how to run automated tests in Scaffold, how the headless test script works, and how to safely edit that script. Scaffold uses an AI-first, automation-first testing workflow. Manually opening Unity is not part of the standard test path documented here.

The primary automated path for Edit Mode tests is the repository script `.agents/scripts/run-editmode-tests.ps1`. It runs Unity in batch mode, collects the NUnit XML results, prints a short report to the terminal, and deletes the temporary files it created before exiting.

Analyzer diagnostics are checked with `.agents/scripts/check-analyzers.ps1`, and the full milestone quality gate can be run with `.agents/scripts/validate-milestone.ps1`.

## Recommended Test Workflow

### Headless Edit Mode Tests

Use the PowerShell script when you want to validate Edit Mode tests without opening the Unity Editor interface.

From the repository root:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1"
```

Expected successful output looks like this:

```text
Running Unity EditMode tests...
Project: C:\Users\user\.codex\worktrees\a3c9\Scaffold
Unity:   C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe

Test Report
-----------
Total:   37
Passed:  37
Failed:  0
Skipped: 0
```

If Unity cannot compile the project before tests start, the script prints a `Status: Blocked` report and includes the compiler errors from the Unity log tail.

### Analyzer Diagnostics Check

Run analyzer checks from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"
```

The script outputs parseable lines:

- `TOTAL:<n>` - total deduplicated SCA diagnostics
- `RULE:<code>:<count>` - per-rule counts
- `FILE:<relative-path>:<count>` - per-file counts
- `BLOCKER:<raw error line>` - non-SCA build blockers

`TOTAL:0` with no `BLOCKER:` lines means analyzer quality gates are clean.

### Combined Milestone Validation

For milestone completion checks, run:

```powershell
& ".\.agents\scripts\validate-milestone.ps1"
```

This script runs, in order:

1. `.agents/scripts/run-editmode-tests.ps1`
2. `.agents/scripts/check-analyzers.ps1`

It then prints a combined summary and exits with:

- `0` - tests pass and analyzer checks are clean
- `1` - tests failed or were blocked
- `2` - analyzer diagnostics remain or blockers were found
- `3` - both test and analyzer gates failed

### Bug Fix Regression Protocol

When a bug is found, treat regression coverage as mandatory.

Use this sequence:

1. Reproduce the bug with a new or updated automated test.
2. Run the test and confirm it fails before the code fix.
3. Implement the bug fix.
4. Run the same test again and confirm it passes.
5. Run the full milestone gate with:

```powershell
& ".\.agents\scripts\validate-milestone.ps1"
```

6. Only complete the milestone when the regression test is passing and the milestone gate is clean.

This process prevents bug reintroduction and keeps fixes observable in CI-like local validation.

## Script Parameters

The script accepts these parameters:

- `-ProjectPath`
  Uses a specific Unity project path instead of the current directory.
- `-UnityPath`
  Uses a specific `Unity.exe` path instead of auto-detecting it from `ProjectSettings/ProjectVersion.txt`.
- `-AssemblyNames`
  Restricts the Edit Mode run to one or more test assemblies.

Example:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" `
  -AssemblyNames "Scaffold.MVVM.Tests","Scaffold.Events.Tests"
```

## How The Script Works

The script follows this sequence:

1. Resolve the project path.
2. Read `ProjectSettings/ProjectVersion.txt`.
3. Auto-detect the matching Unity Editor installation from the Unity Hub install path.
4. Create a temporary folder under the system temp directory.
5. Run Unity with:
   - `-batchmode`
   - `-accept-apiupdate`
   - `-runTests`
   - `-testPlatform EditMode`
   - `-testResults <temp xml path>`
   - `-logFile <temp log path>`
6. Parse the NUnit XML output.
7. Print a terminal report with total, passed, failed, and skipped counts.
8. Print failed test names and failure messages when failures exist.
9. Delete the temporary folder before exiting.

One important implementation detail is that the script does **not** pass `-quit`. The installed Unity Test Framework package in this project does not reliably run command-line tests when `-quit` is supplied. Unity exits on its own after the run completes.

## Exit Codes

The script uses these exit codes:

- `0`
  Tests ran and all tests passed.
- `1`
  The run was blocked before a results XML file was produced. This usually means compiler errors, a bad Unity path, or another Unity instance holding the project lock.
- `2`
  Tests ran and at least one test failed.

## How To Edit The Script

Edit `.agents/scripts/run-editmode-tests.ps1` when you need to change headless Edit Mode test behavior.

The important sections are:

- `Get-UnityVersion`
  Reads the required Unity version from `ProjectSettings/ProjectVersion.txt`.
- `Resolve-UnityPath`
  Finds the correct `Unity.exe` automatically unless the caller passes `-UnityPath`.
- `Get-TestMessage`
  Extracts readable failure messages from the NUnit XML.
- `$unityArgs`
  Defines the Unity command-line arguments used for the test run.
- `try/finally`
  Ensures the temporary XML and log files are deleted before the script exits.

When editing the script:

1. Keep `-testPlatform EditMode` unless you are intentionally changing the script to cover another platform.
2. Do not reintroduce `-quit` unless you also revalidate that the local Unity Test Framework version still executes tests correctly with it.
3. Keep the temporary artifact directory outside the repository so the script does not leave XML or log files in the worktree.
4. Keep the final `exit $scriptExitCode` outside the `try/finally` block so cleanup still happens.
5. After every script change, run the script once and confirm:
   - the report prints correctly
   - the exit code matches the outcome
   - no new `scaffold-editmode-tests-*` temp folders are left behind

## Troubleshooting

### Another Unity instance is running

Symptom:

```text
It looks like another Unity instance is running with this project open.
```

Fix:

- Stop the Unity process that currently holds the project lock.
- Wait a few seconds for the lock to clear.
- Rerun the script.

### Compiler errors block the run

Symptom:

```text
Status:  Blocked
```

Fix:

- Read the error lines printed under `Details`.
- Fix the compile errors first.
- Rerun the script.

### Unity cannot be found

Fix:

- Install the Unity version listed in `ProjectSettings/ProjectVersion.txt`.
- Or pass `-UnityPath` explicitly.

## Related Files

- `.agents/scripts/run-editmode-tests.ps1`
- `.agents/scripts/check-analyzers.ps1`
- `.agents/scripts/validate-milestone.ps1`
- `Architecture.md`
- `AGENTS.MD`
