# Testing

## Purpose

This guide explains how to execute automated checks in this repository and how the PowerShell validation scripts behave.

For test authoring standards, coverage strategy, examples, and best practices, see [AutomatedTesting.md](AutomatedTesting.md).

## Testing suite configuration

Coverage default assembly filters and asmdef audit rules (excluded GUIDs or assembly name strings, and wildcard patterns for first-party script assemblies) are loaded from `.agents/TestingSuite.config.json`. Copy `.agents/TestingSuite.config.example.json` when adding a second product prefix (for example `NewGameProject.*`); the loader is `.agents/TestingSuite.Config.ps1` (`Get-TestingSuiteConfig`). If the JSON file is absent, scripts use the same built-in defaults as before.

## Importing this testing suite into another repo

1. Copy `.agents/` (scripts, workflows, optional pragma allowlist) and `.agents/TestingSuite.config.json` (or start from `TestingSuite.config.example.json`).
2. Set `asmdefReferences.firstPartyAssemblyNamePatterns` and `coverage.defaultAssemblyFilters` to your first-party assembly name prefixes (this repo uses `Scaffold.*` only; a game repo might list `Scaffold.*` and `NewGameProject.*`).
3. Open the Unity project once so it regenerates root `.sln` / `.csproj` files (ignored by git in the default Unity `.gitignore`); without a solution at the repo root, `check-analyzers.ps1` skips the solution build step and reports `TOTAL:0` with a note.
4. Run `validate-changes.cmd` from the host project root on Windows with PowerShell available, or invoke the individual `.ps1` scripts as documented below.

Unity editor version expectations are defined in `ProjectSettings/ProjectVersion.txt` on each machine; the batch scripts resolve the editor from that file (or `UNITY_PATH` / `-UnityPath`), not from a hardcoded install path in the repo.

## Running Tests And Gates

Run from repository root:

```powershell
& ".\.agents\scripts\validate-changes.cmd"
```

### PowerShell 5.x and command chaining

Windows PowerShell 5.x (common default on Windows) does **not** support `&&` as a statement separator; using it fails parsing before any script runs. PowerShell 7+ supports `&&`.

- **Simplest**: stay at the repository root and run the gate as above (no `cd` needed).
- **Chain in PS 5**: use `;` instead of `&&`, or delegate to cmd when you need `&&` short-circuiting:

```powershell
Set-Location "<repository-root>"; cmd /c ".agents\scripts\validate-changes.cmd"
```

Replace `<repository-root>` with your clone path (for example `D:\Games\MyUnityGame` or any path that contains spaces).

```powershell
cmd /c "cd /d D:\Games\MyUnityGame && .agents\scripts\validate-changes.cmd"
```

`cd /d` is a **cmd.exe** idiom; in PowerShell use `Set-Location` (or `cd` without `/d`).

Current pipeline order:

1. `.agents/scripts/check-scripts-asmdef-references.ps1`
2. `.agents/scripts/check-pragma-warning-suppressions.ps1`
3. `.agents/scripts/check-unity-compilation.ps1`
4. `.agents/scripts/run-editmode-tests.ps1` (only if compilation precheck passes)
5. `.agents/scripts/run-playmode-tests.ps1` (only if compilation precheck passes)
6. `.agents/scripts/check-analyzers.ps1`
   Analyzer gate order inside this script:
   1. `dotnet test -c Release Analyzers/Scaffold/Scaffold.Analyzers.Tests/Scaffold.Analyzers.Tests.csproj`
   2. `dotnet test -c Release Generators/Scaffold.Mvvm.Analyzers.Tests/Scaffold.Mvvm.Analyzers.Tests.csproj` (if present)
   3. solution analyzer diagnostics build (`dotnet build`)

Coverage collection is fully separate from quality gate execution. `validate-changes.cmd` never generates coverage artifacts.

### Implementation notes (Windows PowerShell 5.x and paths with spaces)

The repository root path may contain **spaces** (for example a folder named `My Game`). On **Windows PowerShell 5.1**, `Start-Process -ArgumentList` does not reliably quote array elements, which can **split** a path into multiple argv tokens and break Unity CLI and `dotnet` invocations.

The scripts apply these mitigations:

| Mechanism | Used by | Purpose |
|-----------|---------|---------|
| **`UnityProcess.ps1`** (`Start-UnityEditorProcess`) | `check-unity-compilation.ps1`, `run-editmode-tests.ps1`, `run-playmode-tests.ps1` | Builds a single `ProcessStartInfo.Arguments` string with **quoted** arguments so `-projectPath` and log paths stay one token. |
| **Child script invocation** | `validate-changes.ps1` (`Invoke-PowerShellScript`) | Runs nested `.ps1` files with **`& $path @params`** instead of `Start-Process`, so `-ProjectPath` with spaces is passed correctly. |
| **`Invoke-CmdDotNet`** (`cmd.exe /c dotnet … > log 2>&1`) | `check-analyzers.ps1` | Runs `dotnet build` / `dotnet test` with merged stdout/stderr to a log file so **exit codes** match real builds and paths with spaces are quoted correctly (no stray `Start-Process` argv quoting). |

Do not remove these without retesting on a **folder path that contains spaces**.

**Pragma gate:** If there is **no `.git`** directory at the project root, `check-pragma-warning-suppressions.ps1` reports `TOTAL:0` and skips diff-based pragma checks. Initialize git (or clone from a remote) to enforce that gate.

For explicit coverage audits, run:

```powershell
& ".\.agents\scripts\run-coverage-audit.cmd"
```

Targeted runs:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1"
& ".\.agents\scripts\run-playmode-tests.ps1"
powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"
powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-scripts-asmdef-references.ps1"
powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-pragma-warning-suppressions.ps1"
```

## Coverage Goals (Practical Targets)
Coverage goals and best practices are documented in [AutomatedTesting.md](AutomatedTesting.md).

## Parameters And Troubleshooting

### Script Parameters

- `check-unity-compilation.ps1`: `-ProjectPath`, `-UnityPath`, `-TimeoutMinutes` (default `10`)
- `run-editmode-tests.ps1`: `-ProjectPath`, `-UnityPath`, `-AssemblyNames`, `-EnableCoverage`, `-CoverageResultsPath`, `-CoverageOptions`, `-TimeoutMinutes` (default `30`)
- `run-playmode-tests.ps1`: `-ProjectPath`, `-UnityPath`, `-AssemblyNames`, `-EnableCoverage`, `-CoverageResultsPath`, `-CoverageOptions`, `-TimeoutMinutes` (default `30`)
- `check-analyzers.ps1`: `-ProjectPath`, `-TimeoutMinutes` (default `10`), `-AnalyzerTestsTimeoutMinutes` (default `10`)
  Default behavior excludes diagnostics from test assemblies. Add `-IncludeTestAssemblies` to include them.
  Emits `BUILD_EXIT` (from `dotnet build`), `TOTAL` (SCA + SCM hits), `BLOCKER:` for non-analyzer errors; **exits 1** if the build failed or any blocker line was reported (analyzer warning count alone does not force exit 1).
- `check-scripts-asmdef-references.ps1`: `-ProjectPath`, `-ScriptsRoot` (default `Assets/Scripts`), `-ExcludedAssemblyNames`, `-ExcludedGuidReferences`
- `check-pragma-warning-suppressions.ps1`: `-ProjectPath`, `-AllowlistPath` (default `.agents/scripts/pragma-warning-disable-allowlist.txt`)
- `run-coverage-audit.ps1`: `-ProjectPath`, `-UnityPath`, `-AssemblyNames`, `-CoverageResultsPath`, `-CoverageAssemblyFilters`, `-KeepCoverageArtifacts`, `-CompilationTimeoutMinutes`, `-EditModeTimeoutMinutes`, `-PlayModeTimeoutMinutes`
- `validate-changes.ps1`: `-ProjectPath`, `-UnityPath`, `-AssemblyNames`, `-CompilationTimeoutMinutes`, `-EditModeTimeoutMinutes`, `-PlayModeTimeoutMinutes`, `-AnalyzerTimeoutMinutes`, `-AnalyzerTestsTimeoutMinutes`

### Exit Codes (`validate-changes`)

- `0`: compilation precheck passed, tests passed, analyzer checks clean
- `1`: compilation or tests failed/blocked
- `2`: analyzer diagnostics/blockers remain
- `3`: test gate and analyzer gate both failed

### Common Failures

- `Scripts have compiler errors`: fix compile errors first.
- Project already open in another Unity process: close it and rerun.
- Timeout: rerun with a larger timeout while investigating the root cause.

## Related Files

- `.agents/scripts/UnityProcess.ps1` (shared `Start-UnityEditorProcess` helper)
- `.agents/scripts/check-unity-compilation.ps1`
- `.agents/scripts/run-editmode-tests.ps1`
- `.agents/scripts/run-playmode-tests.ps1`
- `.agents/scripts/check-analyzers.ps1`
- `.agents/scripts/check-scripts-asmdef-references.ps1`
- `.agents/scripts/check-pragma-warning-suppressions.ps1`
- `.agents/scripts/run-coverage-audit.cmd`
- `.agents/scripts/run-coverage-audit.ps1`
- `.agents/scripts/validate-changes.cmd`
- `.agents/scripts/validate-changes.ps1`
- `Architecture.md`
- `AGENTS.md`
