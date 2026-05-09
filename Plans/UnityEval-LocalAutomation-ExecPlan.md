# Unity Eval — Local Automation (Exec Plan)

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

## High-Level Design

**Batch-first, bridge-as-fallback.**

- Default path: spawn `Unity.exe -batchmode` against the project, run the requested operation, exit. Stable, isolated, reproducible.
- Fallback path: if Unity is already open against this project (project lockfile held), the CLI talks to a small in-Editor companion (`UnityEvalBridge`) over a file-watcher IPC. The Editor stays open; nothing else about the user's session is disturbed.

The CLI auto-selects the mode by checking `<project>/Temp/UnityLockfile` plus a bridge heartbeat file. The user never picks.

```
┌─────────────────┐
│ Claude / user   │
└────────┬────────┘
         ▼
┌─────────────────────────────────────────┐
│ Tools/UnityEval/unity-eval.ps1          │
│ - resolve Unity.exe (project-locked)    │
│ - check Temp/UnityLockfile              │
│ - branch: batch  | bridge               │
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

```
Tools/UnityEval/
  unity-eval.ps1           # primary, Windows
  unity-eval               # bash mirror (built last)
  parse-results.py         # NUnit XML → JSON; log split by severity
  README.md                # usage, troubleshooting

Assets/Editor/UnityEval/
  UnityEvalBridge.cs       # file-watcher: reads inbox/, writes outbox/, runs/
  UnityEvalBridge.asmdef   # Editor-only, no runtime refs
```

Runtime artifacts live under `<project>/Temp/UnityEval/`. This directory is **session-scoped**: Unity gitignores `Temp/` and may wipe it on Editor restart, so artifacts here are for the current session only and must not be relied on for durable storage. Anything that needs to persist (e.g., a saved scene the bridge produced) goes under `Assets/` via the normal `AssetDatabase` save path.

```
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
```

## CLI Surface

Two verbs. Both auto-pick batch vs bridge.

```
unity-eval eval [--mode editmode|playmode|both] [--filter <regex>]
                [--include-warnings] [--raw] [--timeout <sec>]

unity-eval exec <Namespace.Class.Method> [--args <json>]
                [--scene <path>] [--include-warnings] [--raw] [--timeout <sec>]
```

`exec` targets must be real static methods in an Editor script — that's the only authoring surface. No arbitrary code is injected from the CLI.

## Mode Selection Logic

```
1. resolve UnityExePath (see "Project/Instance Targeting" below)
2. if Temp/UnityLockfile exists AND heartbeat.json fresh (<5s):
       → bridge mode
   elif Temp/UnityLockfile exists AND heartbeat stale or missing:
       → bridge unreachable; exit 3 with explanation
   else:
       → batch mode
```

The "stale lockfile + missing bridge" case usually means a Unity crash. We don't try to recover automatically — we surface it.

## Eval Verb

**Batch mode invocation:**
```
Unity.exe -batchmode -nographics -projectPath <abs> \
          -runTests -testPlatform <EditMode|PlayMode> \
          -testResults <run>/results.xml \
          -logFile <run>/full.log
```

`-runTests` triggers a compile pass first (so source generators run) and produces the NUnit XML. We don't pass `-quit` (conflicts with `-runTests`). PlayMode in batch is supported but `--mode playmode` runs without `-nographics` to keep the renderer happy.

**Bridge mode:** writes `{verb: "eval", mode: "...", ...}` to `inbox/`, bridge calls `CompilationPipeline.RequestScriptCompilation()` then `TestRunnerApi.Execute(...)`, streams results to the same `runs/<id>/` layout, drops response in `outbox/`.

## Exec Verb

**Batch mode:**
```
Unity.exe -batchmode -projectPath <abs> \
          -executeMethod <Namespace.Class.Method> \
          -logFile <run>/full.log
```

Args (if any) are written to `<run>/args.json` and the target method reads them via a known helper (`UnityEvalArgs.Read<T>()`). This avoids passing complex JSON through the command line.

**Bridge mode:** invokes the same method via reflection. Bridge ensures `AssetDatabase.SaveAssets()` and `EditorSceneManager.SaveOpenScenes()` are called after the method returns, so authoring results land on disk.

## Output Contract

Single JSON written to stdout *and* mirrored to `runs/<id>/summary.json`:

```json
{
  "mode": "batch" | "bridge",
  "verb": "eval" | "exec",
  "run_id": "20260509-143052-7af2",
  "duration_ms": 12453,
  "compile": { "ok": true, "errors": [], "warnings_count": 12 },
  "tests":   { "passed": 142, "failed": 0, "skipped": 3, "failures": [...] },
  "logs":    { "errors": [...], "warnings_count": 8, "raw_path": "Temp/UnityEval/runs/20260509-.../full.log" },
  "exec":    { "method": "MyTools.Setup.CreateLevel", "ok": true } // exec only
}
```

Default stdout: errors + test failures inline; warnings collapsed to count + path. `--include-warnings` inlines them. `--raw` inlines everything.

**Exit codes:** `0` clean · `1` test failures · `2` compile errors · `3` Unity unreachable / lock conflict / bridge stale · `4` exec method threw / not found · `5` timeout.

## Project / Instance Targeting

This is the source of subtle bugs when multiple Unity installs and multiple open Editors coexist. Three independent guards:

### 1. Resolve the right Unity.exe (version-locked to *this* project)

```
priority order:
  1. $env:UNITY_EDITOR_PATH (explicit override)
  2. C:\Program Files\Unity\Hub\Editor\<v>\Editor\Unity.exe
       where <v> = first line of ProjectSettings/ProjectVersion.txt → m_EditorVersion
  3. fail with a clear error
```

Never fall back to `unity` on PATH — that defeats the point.

### 2. Always pin the project path

Every Unity invocation passes `-projectPath <absolute-path-to-Scaffold>`. Unity does not search; it uses exactly what we give it. This is also what disambiguates among multiple open Unity instances against different projects.

### 3. Verify the bridge belongs to *this* project

The CLI doesn't trust "any Unity is running." On startup the bridge writes:

```json
// Temp/UnityEval/heartbeat.json
{
  "project_path": "C:\\Users\\…\\Scaffold",
  "unity_version": "6000.3.11f1",
  "process_id": 12345,
  "bridge_version": "1",
  "timestamp": "2026-05-09T14:30:52Z"
}
```

CLI checks: heartbeat exists, `project_path` matches the CLI's resolved path, `unity_version` matches `ProjectVersion.txt`, `timestamp` is fresh (<5s). Any mismatch → fall through to "bridge unreachable, exit 3."

This means even if you have 4 Unity instances open across 4 different projects, the CLI only ever talks to the one that has *this* project loaded.

## Constraints We Accepted

- **Cold-start floor.** Warm Library: ~10–20s per batch run. Cold: 30–90s. No way around this in batch.
- **PlayMode in batch is fine through tests, fragile for free-form sessions.** `--mode playmode` only supports the test framework path.
- **Bridge requires the in-Editor companion to be loaded.** First-time setup needs Unity to recompile so `UnityEvalBridge` gets registered. Documented in README.
- **Editor stays open.** Bridge never calls `EditorApplication.Exit`. No `shutdown` verb in the CLI.
- **No multi-project parallelism on the same project folder.** Unity's lock prevents this; we don't try to work around it.
- **Timeouts must clean up.** When `--timeout` fires (exit 5), the CLI must terminate the spawned Unity process tree (batch mode) or cancel the in-flight bridge command (bridge mode), flush whatever logs were produced into the run dir, and write `summary.json` with `status: "timed_out"`. Bridge mode never kills the Editor process — only the pending command. This guarantee is what prevents a timeout from leaving the project lock held and bricking subsequent runs.

## Build Order

1. **Unity-path resolver + project-targeting guard** (`unity-eval.ps1` skeleton; reads `ProjectVersion.txt`, resolves Hub path, env override).
2. **Batch eval** (`unity-eval eval` end-to-end against batch mode: compile, tests, NUnit XML, log capture).
3. **Batch exec** (`unity-eval exec` via `-executeMethod`; args via `args.json` helper).
4. **`parse-results.py`** (NUnit XML → JSON; log split into errors/warnings/info; severity classification).
5. **Bridge + asmdef** (`UnityEvalBridge.cs`, file-watcher inbox/outbox, heartbeat, eval+exec handlers, domain-reload survival via `SessionState`).
6. **README** (install, usage, exit codes, troubleshooting).
7. **Bash mirror** (`unity-eval` shell script, same surface, for non-Windows dev).
8. **Validation pass** (see below).

After step 5, **redeploy is not relevant** (this isn't the GraphFlow source generator) — the bridge is a normal Editor script, picked up by Unity's compile-on-import.

## Validation Plan (Step 8)

Goal: prove every path works, prove the project/instance guard works, leave a small set of repeatable smoke tests.

### Pre-flight: instance/project targeting

Run *before* anything else. These don't need Claude — they're for the user to spot-check.

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
   - Expect: exit 0, `compile.ok: true`, `tests.failed: 0`, `mode: "batch"`.

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
    - Expect: the second one waits (or exits with a clear "another unity-eval run is in progress against this project" message), never collides on `runs/<id>/`. Run IDs are unique.

13b. **Timeout cleanup:** run an `exec` whose target sleeps longer than `--timeout`.
    - `unity-eval exec MyTools.Setup.SleepForever --timeout 5`
    - Expect: exit 5; in batch mode the spawned Unity process is gone (verify via Task Manager / `Get-Process Unity`); `summary.json` has `status: "timed_out"`; immediately re-running `unity-eval eval` succeeds (proves no orphaned lock). In bridge mode the Editor is still alive and responsive.

### Output-shape sanity

14. `summary.json` for every run is valid JSON, parseable by `python -c "import json,sys; json.load(open(sys.argv[1]))" Temp/UnityEval/runs/<id>/summary.json`.
15. `runs/` is session-scoped: within one Unity session, run IDs are unique and earlier runs remain readable until Unity wipes `Temp/`. We do not require persistence across Editor restarts; if a previous run's artifacts are needed long-term, copy them out before restarting Unity.

### Acceptance checklist

The tool is "done" when, on a Windows machine with Scaffold and Unity 6000.3.11f1 installed:

- [ ] Tests 1–4 pass (project/instance targeting verified).
- [ ] Tests 5–8 pass (eval works in both modes, errors are surfaced).
- [ ] Tests 9–11 pass (exec works, throws are surfaced).
- [ ] Tests 12–13b pass (lock conflicts handled gracefully; timeouts clean up).
- [ ] Tests 14–15 pass (output shape is consistent).
- [ ] Running `unity-eval eval` while Unity is open *does not* close or modify the user's session.
- [ ] Running `unity-eval eval` while Unity is closed leaves Unity closed afterward.
- [ ] README.md documents: install, the two verbs, exit codes, the four troubleshooting cases (stale lockfile, wrong Unity version, missing bridge, exec method not found).
