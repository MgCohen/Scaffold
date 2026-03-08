---
description: run the Scaffold analyzer diagnostics against the full solution, report errors by rule, fix them iteratively, and clean up artifacts
---

When invoked with `/run-tests`, execute the following steps autonomously. Do not stop to ask the user for input unless a fix requires an architectural decision outside the scope of the error message.

> **Note:** The analyzer DLL is assumed to be already built. Only rebuild it (via the `create-custom-analyzer` workflow or `dotnet build -c Release` in the analyzer directory) when analyzer source files have changed.

---

## Step 1 — Run the Full Solution Build

Build the full solution and capture **all** output (stdout + stderr). This is what surfaces analyzer diagnostics:

```bash
dotnet build "C:/Users/user/Documents/Unity/Scaffold/Scaffold.sln" 2>&1
```

---

## Step 2 — Parse and Report Diagnostics

From the captured output, extract every line matching the pattern:
```
: warning SCA\d+:
: error SCA\d+:
```

Do **not** count non-SCA build errors (e.g. CS0006, CS0246) in the analyzer report — treat those as build blockers and fix them first.

Compute:
- **Total** SCA diagnostic count
- **Per-rule** counts (e.g. `SCA0003`, `SCA0005`, `SCA0006`)

Report to the user in this format before fixing anything:

```
Analyzer Diagnostics Report
----------------------------
Total: X diagnostics

SCA0003: N  — Nested call/object construction
SCA0005: N  — Line break inside statement
SCA0006: N  — Method too long
...
```

---

## Step 3 — Fix All Diagnostics

Work through every diagnostic from the build output, file by file:

1. Read the file at the path given in the diagnostic.
2. Navigate to the exact line and column.
3. Apply the fix described verbatim in the diagnostic message — the message always describes exactly what to do.
4. Save the file.
5. Move on to the next diagnostic.

Group edits by file to minimise redundant reads. After processing all diagnostics, move to Step 4.

---

## Step 4 — Verify

Re-run Step 1. Compare the new diagnostic count to the previous count.

- If new SCA diagnostics remain, repeat Step 3 for only the remaining issues.
- Repeat this loop until the build output contains **zero** SCA-prefixed diagnostics.
- If a fix introduces a regression (new diagnostic at a different site), fix that too.

---

## Step 5 — Cleanup

Delete only `msbuild.binlog` in the project root — the one file `dotnet build` can create in the working directory. Do not delete anything else; other artifacts belong to other flows.

```bash
rm -f "C:/Users/user/Documents/Unity/Scaffold/msbuild.binlog"
```

---

## Step 6 — Final Report

Report the outcome to the user:

```
Run-tests complete.
Fixed: X diagnostics across Y files.
Rules cleared: SCA0003, SCA0005, SCA0006, ...
Remaining: 0
```

If any diagnostics could not be fixed (e.g. require a structural refactor beyond the scope of a single fix), list them explicitly and explain why.
