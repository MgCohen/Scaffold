---
description: build the solution and report all analyzer diagnostics by rule, without making any fixes
---

When invoked with `/check-analyzers`, run the build and report diagnostics only. Do not modify any files.

---

## Step 1 - Run the Script

Use the PowerShell script in this Windows workspace (do not use `/bin/bash` here):

```powershell
powershell -ExecutionPolicy Bypass -File "C:/Users/user/Documents/Unity/Scaffold/.agents/scripts/check-analyzers.ps1"
```

If you are on a Unix-like shell, the bash equivalent remains available:

```bash
bash "C:/Users/user/Documents/Unity/Scaffold/.agents/scripts/check-analyzers.sh"
```

Both scripts build with `--no-incremental`, deduplicate identical diagnostics (the same issue can appear twice when a project is compiled as both a standalone target and a dependency), and emit parseable lines:

- `TOTAL:<n>`
- `RULE:<code>:<count>`
- `FILE:<relative-path>:<count>`
- `BLOCKER:<raw error line>` - non-SCA build errors, if any

---

## Step 2 - Report Diagnostics

Format the script output for the user:

```text
Analyzer Diagnostics Report
----------------------------
Total: X diagnostics

SCA0003: N  - Nested call/object construction
SCA0005: N  - Line break inside statement
SCA0006: N  - Method too long
...

Files affected:
- path/to/File.cs  (N issues)
- ...
```

List any `BLOCKER:` lines separately as build blockers.

If TOTAL is 0, report that the build is clean.
