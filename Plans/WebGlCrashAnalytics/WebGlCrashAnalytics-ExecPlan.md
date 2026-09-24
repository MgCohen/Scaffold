# Add recoverable WebGL crash analytics

This ExecPlan is a living document maintained according to `PLANS.md` at the repository root.

## Purpose / Big Picture

Unity Cloud Diagnostics does not support WebGL, and a mobile browser process that is killed cannot make a final network request. After this change, a Scaffold consumer can persist WebGL session checkpoints in browser storage, recognize that the previous session ended abruptly, convert the previous session into a strongly typed Unity Analytics event on the next successful launch, and force an immediate Analytics upload. Gear Engine can use the API with its existing loading-page diagnostics to identify the last phase reached before an iOS Safari termination.

## Progress

- [x] Confirm Unity Cloud Diagnostics excludes WebGL and Unity Analytics supports WebGL.
- [x] Isolate Scaffold work on branch `codex/webgl-crash-analytics`.
- [x] Add the Scaffold WebGL crash report contract, browser bridge, Analytics event, and immediate flush API.
- [x] Add focused EditMode tests for abrupt-session parsing, duplicate suppression, retry behavior, and truncation.
- [x] Document the Unity Analytics event schema and consumer integration.
- [x] Integrate Gear Engine with the local Scaffold package implementation and its WebGL template.
- [x] Run focused tests, C# lint, Scaffold validation, Gear Engine compilation, and a WebGL build.

## Surprises & Discoveries

- Observation: The existing Scaffold checkout contains unrelated local changes.
  Evidence: `git status --short` in the main Scaffold checkout lists modified and untracked files, so this work uses an isolated worktree.
- Observation: Unity Analytics rejects custom events whose schema is absent from the Dashboard.
  Evidence: The installed Analytics SDK contract states that a schema must exist or the event is ignored; the README must therefore define the exact event and parameters.
- Observation: The repository validation wrapper contains platform and baseline blockers unrelated to this package.
  Evidence: `validate-changes.sh -SkipTests` reports four existing GraphFlow asmdef GUID issues, expects Unity `6000.3.11f1`, and invokes Windows-only `cmd.exe` for analyzer checks on macOS. Package compilation was instead proven by the Gear Engine consumer tests and successful WebGL IL2CPP build with Unity `6000.5.9f1`.

## Decision Log

- Decision: Report an abrupt WebGL session on the next successful Unity launch rather than attempting an unload-time upload.
  Rationale: iOS can terminate Safari without running JavaScript cleanup handlers, while `localStorage` survives and Unity Analytics can flush the recovered report next session.
  Author: Codex
- Decision: Add a separate `IWebGlCrashReporter` contract and a general `Flush` method to `IAnalyticsService`.
  Rationale: Browser crash recovery is platform-specific and should not overload the generic analytics recording contract.
  Author: Codex
- Decision: Keep loader checkpoints in the host WebGL template and runtime recovery in Scaffold.
  Rationale: Package JavaScript starts only after Unity loads and cannot observe loader-download or WebAssembly-instantiation failures by itself.
  Author: Codex

## Outcomes & Retrospective

The Scaffold package now provides a recoverable WebGL crash reporter, a browser bridge with host-template and runtime-only modes, immediate Analytics flushing, acknowledgement after successful delivery, and retry behavior when delivery fails. Six focused EditMode tests pass in the Gear Engine consumer, the updated startup wiring test passes, both changed C# scopes pass deterministic lint, the JavaScript bridge passes syntax checking, and a complete WebGL IL2CPP build exits successfully with all five bridge symbols linked. The package README defines the Dashboard schema that must be created before events can be accepted.

## Context and Orientation

The Scaffold analytics package is rooted at `Assets/Packages/com.scaffold.analytics`. `Runtime/Abstraction/IAnalyticsService.cs` is the public recording contract and `Runtime/Implementation/AnalyticsService.cs` adapts it to `com.unity.services.analytics`. `Container/AnalyticsInstaller.cs` registers package services with VContainer. The new WebGL crash reporter will read a JSON report exposed by a host page or the package JavaScript bridge, parse only bounded fields, record a `webglAbruptSession` event, clear the recovered report after successful recording, and flush Analytics immediately.

Gear Engine already exposes `window.gearEngineDiagnostics.getReport()` from `Assets/WebGLTemplates/GearEngine/index.html`. The Gear Engine integration will invoke Scaffold recovery after UGS Analytics initialization and will preserve the loader diagnostics route for direct device inspection.

## Plan of Work

First add an Analytics flush operation and the `WebGlAbruptSessionEvent` parameter contract. Add a small bridge interface so parsing and reporting can be tested without a browser, plus a WebGL `.jslib` implementation that reads the host report and acknowledges delivery. Implement `WebGlCrashReporter` with guard clauses, JSON validation, field length bounds, duplicate suppression, error logging, and a safe false result when no abrupt session exists.

Next add an Editor test assembly for the analytics package. Tests will inject fake browser and analytics boundaries and verify that a valid abrupt report records exactly one event and flushes, normal exits record nothing, duplicate report IDs are suppressed, and oversized text is truncated.

Finally document the public API and exact Dashboard schema, then point Gear Engine at the Scaffold worktree package while validating. Gear Engine will call recovery immediately after Analytics starts, ensuring that a report persisted by the previous Safari process is sent as early as possible.

## Concrete Steps

From the Scaffold worktree, run the focused tests with:

    unity test . --mode EditMode --filter "Scaffold.Analytics.Tests" --output Artifacts/TestResults/WebGlCrashAnalytics.xml --timeout 600

Run deterministic C# formatting against only changed C# files in fix and check modes, then run:

    ./.agents/scripts/validate-changes.sh -SkipTests

From Gear Engine, run the focused startup tests and build the WebGL submission player using the existing exporter. A successful build must reach `Exiting batchmode successfully now!` without C# compiler errors.

## Validation and Acceptance

The focused Scaffold tests must pass and prove that one abrupt previous session becomes one `webglAbruptSession` event followed by one immediate flush and acknowledgement. Normal or already acknowledged sessions must not record. Scaffold validation must finish cleanly.

The Gear Engine WebGL template must expose the previous diagnostic report, and the startup initializer must call the new reporter after Analytics initialization. The resulting hosted player must still reach the campaign screen. When a stored report has `previousEndedAbruptly: true`, the next successful session must enqueue the Analytics event and clear the pending browser report.

## Idempotence and Recovery

Report acknowledgement uses the session ID, so restarting initialization does not duplicate the same report. If Analytics recording throws, the pending report remains in browser storage for the next launch. Removing the Gear Engine package override returns the consumer to the released Scaffold package without modifying player data.

## Artifacts and Notes

The Unity Analytics Dashboard must define the custom event before deployment. The package README will contain the authoritative event name, parameters, and types.

## Interfaces and Dependencies

`IAnalyticsService` exposes `void Flush()`. `IWebGlCrashReporter` exposes `bool TryReportPreviousSession()` and `void SetCheckpoint(string phase, string details = null)`. `WebGlCrashReporter` depends on `IAnalyticsService` and an internal browser bridge. The package continues to depend only on VContainer, UnityEngine, and `com.unity.services.analytics`.

Revision note: Updated after implementation and verification. The repository-wide validation blockers are documented separately from the clean package tests, lint checks, and successful WebGL build.
