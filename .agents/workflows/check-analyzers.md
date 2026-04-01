---
description: build the solution and report all analyzer diagnostics by rule, without making any fixes
---

When invoked with `/check-analyzers`, run the build and report diagnostics only. Do not modify any files.

---

## Step 1 - Run the Script

Use the PowerShell script in this Windows workspace (do not use `/bin/bash` here):

```powershell
powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"
```

By default, test assemblies are excluded. To include them:

```powershell
powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1" -IncludeTestAssemblies
```

Paths with spaces: `Start-Process` passes the `.sln` / `.csproj` path as a single argument without shell-style extra quotes (wrapping in quotes breaks `dotnet`).

If you are on a Unix-like shell and a `check-analyzers.sh` wrapper exists in this repo, use it; otherwise run the PowerShell script above from the repo root.

It runs analyzer unit tests first, then builds the solution with `--no-incremental`, deduplicates identical diagnostics, and emits parseable lines:

- `BUILD_EXIT:<code>` — `dotnet build` exit code (`0` = success)
- `TOTAL:<n>` — combined **SCA** + **SCM** (MVVM) analyzer hits (warnings/errors)
- `RULE:<code>:<count>`
- `FILE:<relative-path>:<count>`
- `DIAG:<raw line>` — one per deduplicated analyzer diagnostic
- `BLOCKER:<raw error line>` — compiler or other errors (not `SCA`/`SCM` codes; excludes `MSB`)

The script **exits with code 1** if the solution build failed or any `BLOCKER:` line was emitted. Non-zero `TOTAL` alone does not fail the script (analyzer violations may be warnings while the build still succeeds).

---

## Step 2 - Report Diagnostics

Format the script output for the user:

```text
Analyzer Diagnostics Report
----------------------------
Total: X diagnostics

SCA2002: N  - Nested call/object construction
SCA1003: N  - Line break inside statement
SCA2003: N  - Method too long
...

Files affected:
- path/to/File.cs  (N issues)
- ...
```

List any `BLOCKER:` lines separately as build blockers.

If `BUILD_EXIT` is `0` and there are no `BLOCKER:` lines, the solution compiled. If `TOTAL` is 0, there were no **SCA**/**SCM** diagnostics in the filtered output.
