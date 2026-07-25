# Unity Eval — Local Automation (Exec Plan)

## Handover Preamble

This document is a self-contained implementation brief for a local development agent (one with access to a Windows host running Unity 6000.3.11f1 against this Scaffold project). It captures every decision made during the design conversation that produced it, so the implementing agent does **not** need to re-derive the architecture, re-litigate rejected alternatives, or ask the user for clarification on intent before starting.

**What you (the implementing agent) need to know up front:**

- The original conversation produced this plan but could not validate any of it — there was no Windows host or Unity install available.
- All Unity-side validation must happen on the implementing host. The design has been pressure-tested on paper and survived a CodeRabbit review (PR #53), but first-run reality will surface bugs.
- **You have access to things the planner did not:** running Unity, the actual `ProjectVersion.txt`, real cold-start timings, the GraphFlow source generator's actual behavior in batch vs interactive mode. Use this access to confirm assumptions early — don't ship code that's only paper-correct.
- **Branch:** all work goes on `claude/unity-eval-automation-bEf31` (already created and tracking origin). PR #53 is open against `main` with this plan as the only commit.
- **First action when you pick this up:** read the "First-Run Checklist for the Implementing Agent" at the bottom of this doc. It walks you through pre-flight sanity checks before you write any code.

## Goal

A single CLI Claude can call locally to perform full Unity evaluation and authoring without manual involvement. Eliminates three current failure modes:

1. Validation blocked because Unity is open against the project.
2. Partial validation because Unity is closed and changes were not compiled.
3. Claude asking the user to "open Unity / run tests / check the console."

## Problem Shape

Two workflows need to be automated:

- **Eval** — compile the project (incl. source generators), run tests, capture errors/warnings/logs.
- **Authoring** — open a scene, create/modify objects or assets, save, optionally run a command (PlayMode test or static editor method), capture logs.

Both must work whether Unity is currently open or closed, without the user being asked to change Unity's state.

## Decisions Already Made (and Alternatives Rejected)

These are locked. Don't reopen unless you find concrete evidence the decision is wrong.

### 1. Batch-first, bridge-as-fallback (locked)

- **Chosen:** spawn `Unity.exe -batchmode` by default; only use the in-Editor bridge when Unity is already open against this project.
- **Rejected — bridge-first:** would require maintaining the bridge's domain-reload survival on the hot path; batch mode is simpler and more isolated.
- **Rejected — batch-only:** can't run while Unity is open against the same project (lockfile conflict). User explicitly does not want to be asked to close Unity.
- **Rationale:** batch is more stable, isolated, reproducible. Bridge exists only because the user often has the Editor open during development.

### 2. File-watcher IPC (not HTTP, not named pipes)

- **Chosen:** poll `Temp/UnityEval/inbox/*.json` from `EditorApplication.update`, write responses to `Temp/UnityEval/outbox/<id>.json`.
- **Rejected — named pipes / Unix sockets:** cross-platform pain; domain reload kills the listener mid-call.
- **Rejected — local HTTP server:** port conflicts; `HttpListener` is awkward in Editor; Windows firewall prompts.
- **Rationale:** zero deps, survives domain reload trivially, works identically on macOS/Win/Linux, easy to debug (just look at the files). ~250ms poll latency is acceptable.

### 3. Editor stays open (locked)

- Bridge **never** calls `EditorApplication.Exit`.
- There is **no `shutdown` verb** in the CLI.
- Even when the CLI launched batch Unity itself, batch mode exits naturally — we don't need an explicit shutdown command.

### 4. Test scope (locked)

- `unity-eval eval --mode editmode|playmode|both`, default `editmode`.
- PlayMode in batch is supported only via the test framework path (`-runTests -testPlatform PlayMode`). Free-form Play sessions in batch are out of scope.

### 5. Log handling (locked)

- Bridge/CLI writes the **full** Editor log slice to disk per run.
- `parse-results.py` splits it into `errors.log` / `warnings.log` / `info.log`.
- Default stdout shows errors inline; `--include-warnings` adds warnings; `--raw` adds everything.
- This was an explicit user preference: "log all and separate by type at the tool level, save, and give Claude only the errors with full logs as backup if needed."

### 6. Project/instance targeting (locked)

- Resolve Unity.exe via `ProjectSettings/ProjectVersion.txt` → Hub path. Env override `UNITY_EDITOR_PATH`.
- **Never** fall back to `unity` on PATH.
- Bridge writes a heartbeat with `project_path`, `unity_version`, `process_id`, `bridge_version`, `timestamp`. CLI verifies all four before trusting the bridge.
- This is the answer to "what if I have multiple Unity instances open from different projects?" — see the "Project / Instance Targeting" section below.

### 7. Stale-lockfile handling — surface, don't auto-recover (locked)

- If `Temp/UnityLockfile` exists but the bridge heartbeat is stale or missing, exit 3 with a clear message. Don't auto-delete the lockfile.
- **Rejected — auto-recovery:** a "stale" lock is most often a Unity that's mid-startup (cold `Library/`, big package import), not a crash. Auto-deleting can corrupt the project. Process-enumeration prechecks on Windows are racy.
- The automation goal is "Claude doesn't ask the user to open/close Unity," not "Claude recovers from Unity crashes." These are different bars. Manual `del Temp\UnityLockfile` is the right fix for actual crashes.

### 8. MCP relationship (informational, locked)

- The bridge is also the foundation for any future Unity MCP server. MCP servers for Unity all assume an interactive Editor (Selection, SceneView, undo grouping, GUI windows are all degraded headless).
- Don't spend effort on "MCP-readiness" now, but don't design the bridge in a way that would block adding MCP later (e.g., keep the inbox/outbox protocol versioned and JSON, don't hard-code single-call semantics).

## High-Level Design

```text
┌─────────────────┐
│ Claude / user   │
└────────┬────────┘
         ▼
┌─────────────────────────────────────────┐
│ Tools/UnityEval/unity-eval.ps1          │
│ - resolve Unity.exe (project-locked)    │
│ - check Temp/UnityLockfile + heartbeat  │
│ - branch: batch  |  bridge              │
└────────┬─────────────────┬──────────────┘
         ▼                 ▼
┌─────────────────┐  ┌──────────────────────────────┐
│ Unity batch     │  │ Live Editor + UnityEvalBridge│
│ -projectPath .  │  │ Temp/UnityEval/inbox→outbox  │
│ -runTests / etc │  │                              │
└────────┬────────┘  └──────────────┬───────────────┘
         └──────────┬───────────────┘
                    ▼
        Temp/UnityEval/runs/<run-id>/
        ├─ full.log
        ├─ errors.log / warnings.log / info.log
        ├─ results.xml
        └─ summary.json   ← stdout JSON mirrors this
```

## File Layout

```text
Tools/UnityEval/
  unity-eval.ps1           # primary, Windows
  unity-eval               # bash mirror (built last)
  parse-results.py         # NUnit XML → JSON; log split by severity
  README.md                # usage, troubleshooting

Assets/Editor/UnityEval/
  UnityEvalBridge.cs       # file-watcher: reads inbox/, writes outbox/, runs/
  UnityEvalArgs.cs         # static helper for exec methods to read args.json
  UnityEvalBridge.asmdef   # Editor-only, no runtime refs except UnityEditor + JsonUtility
```

Runtime artifacts live under `<project>/Temp/UnityEval/`. This directory is **session-scoped**: Unity gitignores `Temp/` and may wipe it on Editor restart, so artifacts here are for the current session only and must not be relied on for durable storage. Anything that needs to persist (e.g., a saved scene the bridge produced) goes under `Assets/` via the normal `AssetDatabase` save path.

```text
Temp/UnityEval/
  heartbeat.json           # bridge writes every 2s while loaded
  inbox/                   # CLI drops <id>.json command files here
  outbox/                  # bridge drops <id>.json responses here
  runs/<run-id>/           # one dir per CLI invocation
    full.log
    errors.log
    warnings.log
    info.log
    results.xml
    summary.json
    args.json              # exec only: input args for the target method
  cli.lock                 # CLI's per-project serialization lock (see Concurrency)
```

## CLI Surface

Two verbs. Both auto-pick batch vs bridge.

```text
unity-eval eval [--mode editmode|playmode|both] [--filter <regex>]
                [--include-warnings] [--raw] [--timeout <sec>]

unity-eval exec <Namespace.Class.Method> [--args <json-or-@file>]
                [--scene <path>] [--include-warnings] [--raw] [--timeout <sec>]
```

Plus utility flags any verb supports:

```text
--print-resolved-path     # print resolved Unity.exe and exit
--print-mode              # print "batch" or "bridge" for current state and exit
--run-id <id>             # override generated run id (testing)
```

`exec` targets must be real static methods in an Editor script — that's the only authoring surface. No arbitrary code is injected from the CLI.

## Mode Selection Logic

```text
1. resolve UnityExePath (see "Project/Instance Targeting" below)
2. acquire CLI lock at Temp/UnityEval/cli.lock (see Concurrency below)
3. if Temp/UnityLockfile exists AND heartbeat.json fresh (<5s) AND heartbeat fields match:
       → bridge mode
   elif Temp/UnityLockfile exists AND heartbeat stale or missing:
       → bridge unreachable; exit 3 with explanation
   else:
       → batch mode
```

The "stale lockfile + missing bridge" case usually means a Unity crash. We don't try to recover automatically — we surface it.

## Eval Verb

**Batch mode invocation:**

```powershell
& $UnityExe -batchmode -nographics -projectPath $ProjectAbs `
            -runTests -testPlatform $Platform `
            -testResults "$RunDir\results.xml" `
            -logFile "$RunDir\full.log"
```

`-runTests` triggers a compile pass first (so source generators run) and produces the NUnit XML. We don't pass `-quit` (conflicts with `-runTests`). PlayMode in batch is supported but `--mode playmode` runs **without** `-nographics` to keep the renderer happy.

**Unity APIs used in bridge mode:**
- `UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation()` — kicks off compile.
- `CompilationPipeline.compilationFinished` — fires once for the whole batch.
- `CompilationPipeline.assemblyCompilationFinished` — per-assembly; `messages[]` of type `UnityEditor.Compilation.CompilerMessage` with a `type` of `Error`/`Warning`/`Info`.
- `UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.Execute(ExecutionSettings)` — runs tests.
- `ICallbacks` interface for run-start / run-finished / test-finished events.
- Use `Filter` with `testMode = TestMode.EditMode | TestMode.PlayMode` and an optional `testNames` regex.
- Test results: write NUnit-format XML using `ITestResultAdaptor.ToXml()` or the built-in result writer. Match the file format batch mode produces so `parse-results.py` doesn't need two parsers.

## Exec Verb

**Batch mode:**

```powershell
& $UnityExe -batchmode -projectPath $ProjectAbs `
            -executeMethod $Method `
            -logFile "$RunDir\full.log"
```

Args (if any) are written to `<run>/args.json` *before* spawning Unity. The target method reads them via the helper:

```csharp
// Assets/Editor/UnityEval/UnityEvalArgs.cs
public static class UnityEvalArgs
{
    public static T Read<T>() where T : new()
    {
        var path = Path.Combine("Temp", "UnityEval", "runs", _currentRunId, "args.json");
        if (!File.Exists(path)) return new T();
        return JsonUtility.FromJson<T>(File.ReadAllText(path));
    }

    // _currentRunId is provided via env var UNITY_EVAL_RUN_ID set by the CLI
    // before launching Unity (batch) or set on inbox command (bridge).
}
```

**Bridge mode:** invokes the same method via reflection (see skeleton below). Bridge ensures `AssetDatabase.SaveAssets()` and `EditorSceneManager.SaveOpenScenes()` are called after the method returns, so authoring results land on disk. If the method threw, we still flush partial state to logs but skip the save.

**Default behavior:** batch is the default for `exec`, same as `eval`. The original conversation considered making bridge the default for authoring (faster iteration, visible feedback), but the user explicitly asked for batch-default everywhere — "if it works in batch, then it's worth my time looking at it manually."

## IPC Protocol (Inbox / Outbox / Heartbeat)

All JSON. UTF-8, no BOM. Files written atomically (write to `*.tmp` then rename) so the watcher never sees half-written files.

### Heartbeat

Written by the bridge every 2s from `EditorApplication.update`:

```json
{
  "project_path": "C:\\Users\\…\\Scaffold",
  "unity_version": "6000.3.11f1",
  "process_id": 12345,
  "bridge_version": "1",
  "timestamp": "2026-05-09T14:30:52Z"
}
```

CLI rejects the bridge if any of: file missing, `timestamp` older than 5s, `project_path` doesn't match the CLI's resolved project path, `unity_version` doesn't match `ProjectVersion.txt`.

### Inbox command (CLI → bridge)

`Temp/UnityEval/inbox/<run-id>.json`:

```json
{
  "run_id": "20260509-143052-7af2",
  "verb": "eval" | "exec",
  "schema_version": 1,
  "eval": {
    "mode": "editmode" | "playmode" | "both",
    "filter": "FailingTest",
    "timeout_sec": 600
  },
  "exec": {
    "method": "MyTools.Setup.CreateLevel",
    "scene": "Assets/Scenes/Empty.unity",
    "args_path": "Temp/UnityEval/runs/20260509-143052-7af2/args.json",
    "timeout_sec": 600
  }
}
```

Only one of `eval` / `exec` is present per command (matching `verb`).

### Outbox response (bridge → CLI)

`Temp/UnityEval/outbox/<run-id>.json`:

```json
{
  "run_id": "20260509-143052-7af2",
  "status": "ok" | "compile_failed" | "tests_failed" | "exec_threw" | "timed_out" | "internal_error",
  "summary_path": "Temp/UnityEval/runs/20260509-143052-7af2/summary.json"
}
```

The CLI then reads `summary.json` for the full payload — keeps the outbox file tiny and avoids races on large responses.

## Output Contract

Single JSON written to stdout *and* mirrored to `runs/<id>/summary.json`:

```json
{
  "mode": "batch" | "bridge",
  "verb": "eval" | "exec",
  "run_id": "20260509-143052-7af2",
  "status": "ok" | "compile_failed" | "tests_failed" | "exec_threw" | "timed_out" | "internal_error",
  "duration_ms": 12453,
  "compile": { "ok": true, "errors": [], "warnings_count": 12 },
  "tests":   { "passed": 142, "failed": 0, "skipped": 3, "failures": [...] },
  "logs":    {
    "errors": [...],
    "warnings_count": 8,
    "raw_path":      "Temp/UnityEval/runs/20260509-.../full.log",
    "errors_path":   "Temp/UnityEval/runs/20260509-.../errors.log",
    "warnings_path": "Temp/UnityEval/runs/20260509-.../warnings.log",
    "info_path":     "Temp/UnityEval/runs/20260509-.../info.log"
  },
  "exec": { "method": "MyTools.Setup.CreateLevel", "ok": true }
}
```

Notes:
- `status` is **always** present and is the canonical machine-readable outcome. `compile.ok` and `tests.failed` are convenience fields and must be consistent with `status`.
- `tests` is omitted when `verb == "exec"`.
- `exec` is omitted when `verb == "eval"`.
- Default stdout: errors + test failures inline; warnings collapsed to count + path. `--include-warnings` inlines them. `--raw` inlines everything.

**Exit codes:** `0` clean · `1` test failures · `2` compile errors · `3` Unity unreachable / lock conflict / bridge stale · `4` exec method threw / not found · `5` timeout · `6` CLI lock contention (another `unity-eval` is running against this project).

## Project / Instance Targeting

This is the source of subtle bugs when multiple Unity installs and multiple open Editors coexist. Three independent guards:

### 1. Resolve the right Unity.exe (version-locked to *this* project)

```text
priority order:
  1. $env:UNITY_EDITOR_PATH (explicit override)
  2. C:\Program Files\Unity\Hub\Editor\<v>\Editor\Unity.exe
       where <v> = first line of ProjectSettings/ProjectVersion.txt → m_EditorVersion
  3. fail with a clear error
```

Never fall back to `unity` on PATH — that defeats the point. Some users install Unity outside the Hub default; that's what `UNITY_EDITOR_PATH` is for.

### 2. Always pin the project path

Every Unity invocation passes `-projectPath <absolute-path-to-Scaffold>`. Unity does not search; it uses exactly what we give it. This is also what disambiguates among multiple open Unity instances against different projects.

### 3. Verify the bridge belongs to *this* project

The CLI doesn't trust "any Unity is running." It verifies all four heartbeat fields match (see IPC Protocol above). Even with 4 Unity instances open across 4 different projects, the CLI only ever talks to the one that has *this* project loaded.

## Concurrency

Two CLI invocations against the same project must not collide.

**Mechanism:** `Temp/UnityEval/cli.lock` — a file lock acquired before mode selection. Open with `FileShare.None` on Windows (`flock` on Unix for the bash mirror). If acquisition fails:

- If `--wait` is passed (default): block until acquired, with a 30s timeout.
- Otherwise: exit 6 immediately with "another unity-eval run is in progress against this project."

Lock is released when the CLI exits (process death releases the file handle automatically — even on crash).

**Crash recovery:** because we use OS file locking, a crashed CLI does not leave a stale lock. Don't implement PID-file based locking that has to be cleaned up.

## Constraints We Accepted

- **Cold-start floor.** Warm Library: ~10–20s per batch run (estimate from comparable projects; **measure on first run and update this number in the README**). Cold (after package change or fresh `Library/`): 30–90s, occasionally more. No way around this in batch.
- **PlayMode in batch is fine through tests, fragile for free-form sessions.** `--mode playmode` only supports the test framework path.
- **Bridge requires the in-Editor companion to be loaded.** First-time setup needs Unity to recompile so `UnityEvalBridge` gets registered. Documented in README.
- **Editor stays open.** Bridge never calls `EditorApplication.Exit`. No `shutdown` verb in the CLI.
- **No multi-project parallelism on the same project folder.** Unity's lock prevents this; we don't try to work around it.
- **Timeouts must clean up.** When `--timeout` fires (exit 5), the CLI must terminate the spawned Unity process tree (batch mode) or cancel the in-flight bridge command (bridge mode), flush whatever logs were produced into the run dir, and write `summary.json` with `status: "timed_out"`. Bridge mode never kills the Editor process — only the pending command. This guarantee is what prevents a timeout from leaving the project lock held and bricking subsequent runs.

## Risk Areas / Known Unknowns

These are the parts most likely to need iteration on first run. Flagged here so you don't lose a day to surprise:

1. **Unity 6000.3 specifics.** The APIs this plan leans on (`CompilationPipeline`, `TestRunnerApi`, `EditorApplication.Exit`, `SessionState`) have been stable since ~Unity 2020 LTS, but Unity 6 changed package-manager and asset-pipeline behavior in ways that could affect timing. Validate `TestRunnerApi.Execute` callback signatures against the local install before committing to a particular shape — Unity sometimes renames callback parameters between minor versions.

2. **Domain-reload survival in the bridge.** When an `exec` triggers a recompile mid-flight, the AppDomain reloads and any in-flight C# state is lost. The plan calls for `SessionState`-backed resume:
   - On command receipt, write `SessionState.SetString("UnityEval.PendingRunId", runId)` plus a state enum.
   - On `[InitializeOnLoad]` after reload, check `SessionState`; if a run was pending, resume from the next state (e.g., "compile finished, now run tests").
   - This is the single trickiest part of the bridge. Expect at least one round of "first run hangs after domain reload" debugging.

3. **Process-tree kill on timeout (Windows).** Unity spawns child processes (Bee, BeeBackend, package manager) that don't always belong to the same Job Object. PowerShell skeleton uses `Stop-Process -Force` with manual descendant-walk via `Get-CimInstance Win32_Process` (see skeletons below). `taskkill /T /F` is a fallback. Verify in test 13b that no `Unity.exe` survives.

4. **GraphFlow source generator behavior in batch.** The user's `CLAUDE.md` is emphatic about the GraphFlow generator's deployment quirks. In theory both batch and interactive modes invoke the same Roslyn pipeline, so generators run identically. **Verify this** in test 5 (clean batch eval) — if you see GraphFlow-generated symbols missing, the assumption is wrong and we need a different compile-trigger strategy. (Reading `CLAUDE.md` is required — the generator DLL must be present in `Assets/Packages/com.scaffold.graphflow/Generators/` for compile to succeed.)

5. **Heartbeat freshness window (5s).** The Editor stalls during big imports, which can pause `EditorApplication.update`. If you see false-positive "bridge stale" rejections during normal use, raise the window (10s, 15s) — but don't make it longer than 30s or you'll mask actual crashes.

6. **PlayMode test in batch without `-nographics`.** Documented as the support path; on some Unity versions you also need `-force-d3d11` or similar GPU flags. Tune in test 8 if PlayMode tests don't start.

## Repo Conventions

- **Plans go in `Plans/`** with `*-ExecPlan.md` naming (matches `Backend-Manifest-ExecPlan.md`, `Agents-Scripts-Refactor-ExecPlan.md`, etc.).
- **Editor scripts** live in any folder named `Editor/` (Unity convention). This plan creates `Assets/Editor/UnityEval/` for the bridge.
- **asmdef** is required for the bridge so it doesn't leak into runtime builds. Set `includePlatforms: ["Editor"]`.
- **Markdown fences in `Plans/`** are typically untagged — sampling other plans shows ~0/3 use language tags. Don't tag fences in this plan to satisfy a markdown linter; there's no markdown CI in the repo and tagging just one file creates inconsistency.
- **GraphFlow generator reminder** (from `CLAUDE.md`): if you ever touch `Generators/Scaffold.GraphFlow.*/`, you must rebuild the DLL and copy it into `Assets/Packages/com.scaffold.graphflow/Generators/`. This plan does not touch that generator, but be aware: if your validation runs surface compile errors that look like missing generator output, this is the cause.
- **CLAUDE.md `currentDate`** is treated as truth for timestamps; today is 2026-05-09 in this conversation.

## Build Order

Each step should be independently committable, so the user can review incrementally on PR #53.

1. **Unity-path resolver + project-targeting guard** (`unity-eval.ps1` skeleton; reads `ProjectVersion.txt`, resolves Hub path, env override, implements `--print-resolved-path`). Land first because everything downstream depends on it.
2. **CLI lock + run-dir creation** (`Temp/UnityEval/cli.lock` acquisition; `runs/<run-id>/` scaffolding).
3. **Batch eval** (`unity-eval eval` end-to-end against batch mode: spawn Unity, capture exit code, find `results.xml`, copy `full.log` into run dir).
4. **`parse-results.py`** (NUnit XML → JSON; log split into errors/warnings/info; severity classification regex).
5. **Batch exec** (`unity-eval exec` via `-executeMethod`; args via `args.json`; `UnityEvalArgs` helper).
6. **Bridge skeleton + heartbeat** (`UnityEvalBridge.cs` with `[InitializeOnLoad]`, `EditorApplication.update` poll, heartbeat write, asmdef).
7. **Bridge eval handler** (`CompilationPipeline.RequestScriptCompilation`, `TestRunnerApi.Execute`, write same run-dir layout as batch).
8. **Bridge exec handler** (reflection invoke + `AssetDatabase.SaveAssets` + `EditorSceneManager.SaveOpenScenes`).
9. **Domain-reload survival** (`SessionState` resume pattern). Test by triggering a recompile mid-`exec`.
10. **Timeout + process-tree kill** (the cleanup constraint from CodeRabbit review). Validate with test 13b.
11. **README** (install, usage, exit codes, troubleshooting). Include the four troubleshooting cases: stale lockfile, wrong Unity version, missing bridge, exec method not found.
12. **Bash mirror** (`unity-eval` shell script, same surface, for non-Windows dev).
13. **Validation pass** (see below — every test in the validation plan).

After step 6, the bridge is a normal Editor script and Unity's compile-on-import picks it up. The GraphFlow source-generator redeploy dance from `CLAUDE.md` does **not** apply here.

## Skeletons for Tricky Parts

These are starting points, not complete implementations. Adapt to what `dotnet`/Unity actually compile against on first run.

### Bridge entry point + heartbeat

```csharp
// Assets/Editor/UnityEval/UnityEvalBridge.cs
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UnityEvalBridge
{
    static double _nextHeartbeat;
    static double _nextPoll;
    const double HeartbeatInterval = 2.0;
    const double PollInterval = 0.25;

    static UnityEvalBridge()
    {
        EnsureDirs();
        EditorApplication.update += Tick;
        ResumePendingRunIfAny();
    }

    static void Tick()
    {
        var now = EditorApplication.timeSinceStartup;
        if (now >= _nextHeartbeat) { WriteHeartbeat(); _nextHeartbeat = now + HeartbeatInterval; }
        if (now >= _nextPoll)      { PollInbox();    _nextPoll      = now + PollInterval; }
    }

    static void WriteHeartbeat()
    {
        var hb = new {
            project_path  = Path.GetFullPath("."),
            unity_version = Application.unityVersion,
            process_id    = System.Diagnostics.Process.GetCurrentProcess().Id,
            bridge_version = "1",
            timestamp = DateTime.UtcNow.ToString("o")
        };
        WriteAtomic("Temp/UnityEval/heartbeat.json", JsonUtility.ToJson(hb));
    }

    // PollInbox, ResumePendingRunIfAny, command dispatch, etc. — left to implementer
}
```

### Domain-reload survival sketch

```csharp
// In bridge, when starting a long-running operation:
SessionState.SetString("UnityEval.PendingRunId", runId);
SessionState.SetString("UnityEval.PendingState", "compile_started");

// In the static constructor / [InitializeOnLoad]:
static void ResumePendingRunIfAny()
{
    var runId = SessionState.GetString("UnityEval.PendingRunId", "");
    if (string.IsNullOrEmpty(runId)) return;
    var state = SessionState.GetString("UnityEval.PendingState", "");
    // Resume based on state. e.g., "compile_started" → check if compile finished
    // (CompilationPipeline.isCompiling), then transition to test phase.
}

// Clear on completion:
SessionState.EraseString("UnityEval.PendingRunId");
SessionState.EraseString("UnityEval.PendingState");
```

`SessionState` survives domain reload but not Editor restart — perfect for this use case.

### Atomic file write (no half-written JSON)

```csharp
static void WriteAtomic(string path, string content)
{
    var tmp = path + ".tmp";
    File.WriteAllText(tmp, content);
    if (File.Exists(path)) File.Delete(path);
    File.Move(tmp, path);
}
```

PowerShell equivalent: write to `$path.tmp`, then `Move-Item -Force $path.tmp $path`.

### Process-tree kill (PowerShell)

```powershell
function Stop-ProcessTree {
    param([int]$RootPid)
    $children = Get-CimInstance Win32_Process -Filter "ParentProcessId = $RootPid"
    foreach ($c in $children) { Stop-ProcessTree -RootPid $c.ProcessId }
    try { Stop-Process -Id $RootPid -Force -ErrorAction Stop } catch { }
}
```

Call this when `--timeout` fires in batch mode. Verify no `Unity.exe` remains via `Get-Process Unity` in test 13b.

### Log severity classification (parse-results.py)

```python
ERROR_PATTERNS = [
    r"\berror CS\d+\b",
    r"\bAssertion failed\b",
    r"\b(NullReferenceException|InvalidOperationException|ArgumentException|InvalidCastException)\b",
    r"^\s*at [\w.<>+]+\(",     # stack frame following an exception
    r"\bUnhandledException\b",
    r"\bFATAL\b",
]
WARNING_PATTERNS = [
    r"\bwarning CS\d+\b",
    r"\bObsolete\b",
    r"\bDeprecated\b",
]
# Everything else → info.log
```

Apply line by line; first matching pattern wins. Keep dedup ("(×N)") for consecutive identical lines to compress import spam.

### NUnit XML → JSON

Unity's TestRunner emits NUnit3-format XML. Parse with `xml.etree.ElementTree`. Top-level: `<test-run>` → `<test-suite>` → `<test-case>` with `result="Passed"|"Failed"|"Skipped"`. Failures have `<failure>` child with `<message>` and `<stack-trace>`. Map to:

```python
{
  "passed":  int,
  "failed":  int,
  "skipped": int,
  "failures": [
    {"name": "Foo.Bar.Baz", "message": "...", "stack": "..."}
  ]
}
```

## Validation Plan (Step 13)

Goal: prove every path works, prove the project/instance guard works, leave a small set of repeatable smoke tests.

### Pre-flight: instance/project targeting

Run *before* anything else. These don't need Claude — they're for the user (or you, the implementing agent) to spot-check.

1. **Unity-path resolution, no env var:**
   ```pwsh
   Remove-Item Env:UNITY_EDITOR_PATH -ErrorAction SilentlyContinue
   .\Tools\UnityEval\unity-eval.ps1 --print-resolved-path
   ```
   Expect: prints `C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe`.

2. **Env var override wins:**
   ```pwsh
   $env:UNITY_EDITOR_PATH = "C:\some\other\Unity.exe"
   .\Tools\UnityEval\unity-eval.ps1 --print-resolved-path
   ```
   Expect: prints the override.

3. **Wrong-version guard:** Temporarily edit `ProjectSettings/ProjectVersion.txt` to a version not installed.
   Expect: exit 3 with a clear "Unity 6000.X.Yf1 not found at expected Hub path" message. (Revert after.)

4. **Multi-instance disambiguation:**
   - Open Unity against a *different* project.
   - With Scaffold's Editor *closed*, run `unity-eval eval`.
   - Expect: batch mode runs against Scaffold (not the other project), `summary.json` shows `mode: "batch"`, `project_path` matches Scaffold.
   - Then open Scaffold in a second Unity instance and re-run `unity-eval eval`.
   - Expect: `mode: "bridge"`, heartbeat verified, no interaction with the unrelated Unity.

### Smoke tests — eval path

5. **Clean compile, all tests pass (batch):**
   - Close Unity.
   - `unity-eval eval`
   - Expect: exit 0, `compile.ok: true`, `tests.failed: 0`, `mode: "batch"`. **GraphFlow generator must run** — verify by checking that types/symbols generated by `Scaffold.GraphFlow.PackageGenerator` appear in compiled assemblies. If they're missing, see Risk Area 4.

6. **Clean compile, all tests pass (bridge):**
   - Open Unity against Scaffold; wait for the Editor to finish loading (heartbeat present).
   - `unity-eval eval`
   - Expect: same as above but `mode: "bridge"`. Editor is still open afterward.

7. **Compile error is captured:**
   - Add a deliberate `error CS0103` to a small editor script (e.g. `var foo = nonexistentSymbol;`).
   - `unity-eval eval`
   - Expect: exit 2, `compile.ok: false`, `compile.errors[0]` contains the file:line and `CS0103`. Verify `errors.log` includes the same line. Revert.

8. **Test failure is captured:**
   - Add a temporary `[Test] public void FailingTest() => Assert.Fail("intentional");` in a test asmdef.
   - `unity-eval eval --mode editmode --filter "FailingTest"`
   - Expect: exit 1, `tests.failed: 1`, `tests.failures[0].name` contains "FailingTest". Revert.

### Smoke tests — exec path

9. **Trivial authoring round-trip (batch):**
   - Add an editor script with a static method:
     ```csharp
     public static class UnityEvalSmoke {
         public static void CreateMarker() {
             var path = "Assets/_unityEvalSmoke.asset";
             var so = ScriptableObject.CreateInstance<TextAsset>();
             AssetDatabase.CreateAsset(so, path);
             AssetDatabase.SaveAssets();
             Debug.Log($"[UnityEvalSmoke] wrote {path}");
         }
     }
     ```
   - `unity-eval exec UnityEvalSmoke.CreateMarker`
   - Expect: exit 0, asset present at `Assets/_unityEvalSmoke.asset`, `logs.info` contains the `[UnityEvalSmoke]` line. Delete the asset after.

10. **Authoring with scene change (batch):**
    - Static method opens an empty scene, adds a `GameObject` named `EvalProbe`, saves, logs.
    - `unity-eval exec MyTools.Setup.AddProbe --scene Assets/Scenes/Empty.unity`
    - Expect: exit 0; reopening the scene in Unity manually shows `EvalProbe`.

11. **Exec method throws is surfaced:**
    - Static method calls `throw new InvalidOperationException("boom")`.
    - Expect: exit 4, `exec.ok: false`, exception message in `logs.errors`.

### Concurrency / lock-conflict guards

12. **Stale lockfile handling:** with Unity closed, manually create an empty `Temp/UnityLockfile`.
    - `unity-eval eval`
    - Expect: exit 3 with "stale lockfile, no bridge heartbeat — restart Unity or delete `Temp/UnityLockfile`."

13. **Concurrent invocations are serialized:** kick off two `unity-eval eval` calls in parallel.
    - Expect: with `--wait` (default), the second blocks up to 30s and then runs; without `--wait`, it exits 6 with "another unity-eval run is in progress against this project." Run IDs are unique.

13b. **Timeout cleanup:** run an `exec` whose target sleeps longer than `--timeout`.
    - `unity-eval exec MyTools.Setup.SleepForever --timeout 5`
    - Expect: exit 5; in batch mode the spawned Unity process is gone (verify via `Get-Process Unity`); `summary.json` has `status: "timed_out"`; immediately re-running `unity-eval eval` succeeds (proves no orphaned lock). In bridge mode the Editor is still alive and responsive.

13c. **Domain-reload survival (bridge only):** start `unity-eval exec` against a method that takes ~10s, and during that window touch a script file in `Assets/` to trigger recompile.
    - Expect: exec resumes after the domain reload (driven by `SessionState`), `summary.json` reflects completion. If this hangs, see Risk Area 2.

### Output-shape sanity

14. `summary.json` for every run is valid JSON, parseable by `python -c "import json,sys; json.load(open(sys.argv[1]))" Temp/UnityEval/runs/<id>/summary.json`.
15. `runs/` is session-scoped: within one Unity session, run IDs are unique and earlier runs remain readable until Unity wipes `Temp/`. We do not require persistence across Editor restarts; if a previous run's artifacts are needed long-term, copy them out before restarting Unity.

### Acceptance checklist

The tool is "done" when, on a Windows machine with Scaffold and Unity 6000.3.11f1 installed:

- [ ] Tests 1–4 pass (project/instance targeting verified).
- [ ] Tests 5–8 pass (eval works in both modes, errors are surfaced, GraphFlow generator confirmed running).
- [ ] Tests 9–11 pass (exec works, throws are surfaced).
- [ ] Tests 12–13c pass (lock conflicts handled, timeouts clean up, domain reload survived).
- [ ] Tests 14–15 pass (output shape is consistent).
- [ ] Running `unity-eval eval` while Unity is open *does not* close or modify the user's session.
- [ ] Running `unity-eval eval` while Unity is closed leaves Unity closed afterward.
- [ ] README.md documents: install, the two verbs, exit codes, the four troubleshooting cases (stale lockfile, wrong Unity version, missing bridge, exec method not found), and the **measured** cold-start time on the user's machine (replacing the planner's estimate).

## First-Run Checklist for the Implementing Agent

Before you write any code, do these:

1. **Pull the branch:** `git fetch origin && git checkout claude/unity-eval-automation-bEf31`. Confirm the only commit ahead of `main` is the plan + the timeout/scope clarifications (PR #53).
2. **Read `CLAUDE.md`** in repo root. The GraphFlow source-generator deployment workflow is non-obvious and you'll trip on it if you ever modify generators.
3. **Confirm Unity version match:** `Get-Content ProjectSettings\ProjectVersion.txt` → expect `m_EditorVersion: 6000.3.11f1`. If the user is on a newer patch (`6000.3.12f1` etc.), the Hub path resolver still works as long as you read the version dynamically from this file.
4. **Confirm the Hub install:** `Test-Path "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe"`. If false, ask the user where Unity is installed before you go further — `UNITY_EDITOR_PATH` will be needed.
5. **Decide commit cadence with the user.** PR #53 is open and reviewed. Each step in the build order can be its own commit on the same branch; don't batch all 13 steps into one push or review will be impossible.
6. **Skim PR #53 review comments and the resolved threads.** The CodeRabbit review surfaced four points; three are reflected in this plan, one (auto-recovery of stale lockfiles) was deliberately rejected — don't re-implement it.
7. **Start at build order step 1** and don't skip ahead. The Unity-path resolver is a 30-line script and unblocks everything else; getting it wrong wastes hours later.

When in doubt about a design choice not covered in this doc, **prefer the simpler option and flag it in the PR description** rather than asking the user — the user wants forward motion. The genuinely architectural decisions are all in "Decisions Already Made" above.
