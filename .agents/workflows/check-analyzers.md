---
description: build the solution and report all analyzer diagnostics by rule, without making any fixes
---

When invoked with `/check-analyzers`, run the build and report diagnostics only. Do not modify any files.

---

## Step 1 — Run the Full Solution Build

```bash
dotnet build "C:/Users/user/Documents/Unity/Scaffold/Scaffold.sln" 2>&1
```

---

## Step 2 — Report Diagnostics

Extract every line matching `: warning SCA\d+:` or `: error SCA\d+:` from the output.

Report to the user:

```
Analyzer Diagnostics Report
----------------------------
Total: X diagnostics

SCA0003: N  — Nested call/object construction
SCA0005: N  — Line break inside statement
SCA0006: N  — Method too long
...

Files affected:
- path/to/File.cs  (N issues)
- ...
```

If there are non-SCA build errors (e.g. CS0006), list them separately as build blockers.

If there are zero SCA diagnostics, report that the build is clean.
