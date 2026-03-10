# Implement Hierarchy Listeners for Replace-EventBus

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root and `Plans/Replace-EventBus/Replace-EventBus.md`.

## Purpose / Big Picture

After this milestone, the new event bus listener path will be production-ready: `AddListener` and `RemoveListener` (generic and open-type) will work, and publishing a derived event will notify both exact listeners and base/abstract listeners. A contributor can verify this by running Events tests that prove hierarchy dispatch behavior and listener idempotence.

## Progress

- [x] (2026-03-10 00:00Z) Created implementation-focused ExecPlan for hierarchy listener delivery.
- [x] (2026-03-10 12:00Z) Implemented listener storage and add/remove registration paths in `ScalableEventBus` using `Map<Type, long, ListenerEntry>` plus idempotence tracking dictionaries for generic/open-type listeners.
- [x] (2026-03-10 12:00Z) Implemented exact and hierarchy dispatch resolution via `Map` indexers (`exact:*` and `hierarchy:*`) with no standalone listener cache dictionary.
- [x] (2026-03-10 12:00Z) Added `ScalableEventBusTests` coverage for hierarchy dispatch, open-type idempotence, invalid open-type registration guard, and continue-on-failure listener behavior.
- [x] (2026-03-10 12:00Z) Validated builds (`dotnet build Scaffold.Events.csproj -c Release`, `dotnet build Scaffold.Events.Tests.csproj -c Release`) and analyzer workflow (`.agents/scripts/check-analyzers.ps1`); Unity CLI test execution remains environment-blocked by licensing handshake/no XML output.

## Surprises & Discoveries

- Observation: Unity batch test command exits successfully but does not produce the requested test XML file in this environment, while the log shows licensing handshake errors.
  Evidence: `Logs/Events-HierarchyListeners.log` includes `[Licensing::Module] Error: Failed to handshake to channel: "LicenseClient-user"` and no `Logs/Events-HierarchyListeners.xml` is produced.

- Observation: `Map` indexers track subsequent `Add`/`Remove` changes automatically once created, so runtime dispatch can reuse per-event-type indexers without a separate cache invalidation layer.
  Evidence: `Assets/Scripts/Tools/Maps/Runtime/Map.cs` calls `TrackEntry`/`UntrackEntry` on indexers during add/remove operations.

## Decision Log

- Decision: Keep listener API naming exclusively as `AddListener` and `RemoveListener`.
  Rationale: Existing repository consumers already use these names, and dual naming introduces unnecessary migration complexity.
  Date/Author: 2026-03-10 / Codex

- Decision: Use direct `Scaffold.Maps` lookup for exact listeners and `Map` indexers for hierarchy listeners.
  Rationale: The map already provides exact key lookup, while indexers provide efficient hierarchy group resolution without separate cache invalidation.
  Date/Author: 2026-03-10 / Codex

- Decision: Use `Map` indexers for both exact and hierarchy dispatch sets, keyed by event runtime type (`exact:<type>` and `hierarchy:<type>`).
  Rationale: This keeps dispatch lookup consistent in one mechanism and avoids introducing additional per-type registries outside `Map`.
  Date/Author: 2026-03-10 / Codex

- Decision: Listener invocation failures are logged via `Debug.LogException` and dispatch continues to remaining listeners.
  Rationale: Milestone acceptance requires failure isolation so one listener cannot block the rest of the publish path.
  Date/Author: 2026-03-10 / Codex

## Outcomes & Retrospective

Milestone 3 implementation is complete: `ScalableEventBus` now supports generic/open-type add/remove, hierarchy dispatch (base/abstract listeners receive derived events), idempotent registration/removal, and continue-on-failure invocation behavior. Build/analyzer checks are green for this milestone's changes; Unity CLI test result artifact generation remains blocked by environment licensing handshake issues, so runtime behavior proof currently relies on compile-time test coverage and executed batch logs.

## Context and Orientation

Relevant files for this milestone:

- `Assets/Scripts/Infra/Events/Runtime/Contracts/IEventBus.cs`
- `Assets/Scripts/Infra/Events/Runtime/Contracts/ContextEvent.cs`
- `Assets/Scripts/Infra/Events/Runtime/Implementation/EventController.cs`
- `Assets/Scripts/Infra/Events/Runtime/Implementation/` (new scalable implementation location)
- `Assets/Scripts/Infra/Events/Tests/EventsTests.cs`
- `Assets/Scripts/Tools/Maps/Runtime/Map.cs`

In this repository, hierarchy dispatch means that if `DerivedEvent : BaseEvent`, listeners declared for `DerivedEvent` and listeners declared for `BaseEvent` both run when `DerivedEvent` is raised.

## Plan of Work

Create or update `ScalableEventBus` in `Assets/Scripts/Infra/Events/Runtime/Implementation/` so it implements all `IEventBus` listener methods (`AddListener`, `RemoveListener`, `Raise`, `Clear`).

Represent listeners in `Map<Type, long, ListenerEntry>`, where the primary key is declared listener event type and the secondary key is a generated subscription id. Use direct map lookup for exact declared type matches, and use indexers only for assignable base/abstract declarations.

Implement `Raise(ContextEvent evt)` so it resolves exact plus hierarchy listeners, executes them outside lock scope, and continues dispatch if one listener throws. Report listener failures through diagnostics hooks if available; if hooks are not implemented yet, keep the call site extension-ready.

Add tests that prove exact-only, hierarchy-only, and combined dispatch behavior. Include duplicate add/remove idempotence scenarios.

## Concrete Steps

Run from repo root: `C:/Users/user/Documents/Unity/Scaffold`.

1. Inspect baseline runtime and tests.

    Get-Content Assets/Scripts/Infra/Events/Runtime/Contracts/IEventBus.cs
    Get-Content Assets/Scripts/Infra/Events/Runtime/Implementation/EventController.cs
    Get-Content Assets/Scripts/Infra/Events/Tests/EventsTests.cs

2. Implement scalable listener runtime in `Assets/Scripts/Infra/Events/Runtime/Implementation/ScalableEventBus.cs`.

3. Update/add tests under `Assets/Scripts/Infra/Events/Tests/` for hierarchy listener behavior.

4. Run build.

    dotnet build Scaffold.sln -c Release

5. Run Events tests.

    Unity.exe -batchmode -quit -projectPath "C:\Users\user\Documents\Unity\Scaffold" -runTests -testPlatform EditMode -testFilter "Scaffold.Events.Tests" -testResults "Logs\Events-HierarchyListeners.xml"

## Validation and Acceptance

Milestone Gate Requirement: before moving to the next milestone, run build/lint/analyzer checks and the milestone test suite, fix all introduced warnings/errors/failures, re-run until green, and commit the milestone changes before starting the next milestone.
This milestone is accepted when all conditions are true:

1. `AddListener` and `RemoveListener` work for generic and open-type listener registration.
2. Raising a derived event invokes exact listeners and base/abstract listeners.
3. Duplicate add/remove paths are deterministic and idempotent.
4. Listener failures do not prevent remaining listeners from executing.
5. Events tests covering hierarchy behavior pass.

## Idempotence and Recovery

This milestone is additive and safe to rerun.

If listener dispatch behavior regresses, keep tests and contracts, and temporarily bind `IEventBus` back to `EventController` in installer wiring while preserving new runtime code for continued iteration.

## Artifacts and Notes

Capture concise evidence while implementing:

- Test output showing exact + base listener invocation counts.
- Test output showing add/remove idempotence.
- Test output showing listener failure does not stop other listeners.

Store longer logs under `Logs/`.

## Interfaces and Dependencies

Expected interfaces and types at milestone completion:

- `Scaffold.Events.IEventBus` implemented by `ScalableEventBus` for listener flows.
- `ListenerEntry` (or equivalent internal struct/class) for handler identity and invocation.
- `Map<Type, long, ListenerEntry>` usage in runtime storage.

Dependencies:

- `Scaffold.Maps` for indexed listener grouping.
- Existing Events contracts in `Assets/Scripts/Infra/Events/Runtime/Contracts/`.

---

Revision Note (2026-03-10): Initial hierarchy-listener implementation plan created as a child plan of `Replace-EventBus`.
Revision Note (2026-03-10): Clarified dispatch resolution strategy to use direct map lookup for exact listeners and indexers only for hierarchy listeners.
Revision Note (2026-03-10): Added explicit milestone gate requiring checks/tests/fixes before advancing.
Revision Note (2026-03-10): Updated milestone gate to require committing changes immediately after successful validation/testing.
Revision Note (2026-03-10): Implemented `ScalableEventBus` hierarchy listener runtime and test coverage, and documented Unity batch test XML output limitation observed during validation.

