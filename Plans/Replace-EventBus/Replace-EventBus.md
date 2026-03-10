# Replace-EventBus

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this work, Scaffold will have a replacement for `Assets/Scripts/Infra/Events/Runtime/Contracts/IEventBus.cs` that keeps the familiar listener workflow while removing the need for per-event container registrations. The new bus will support both exact and hierarchy dispatch (base and abstract handlers receive derived events), async request/response flows, and middleware hooks for cross-cutting concerns.

A contributor will be able to verify success by running Events tests that prove:

- Existing `AddListener`/`RemoveListener`/`Raise` flows still work.
- Unified `AddListener`/`RemoveListener` APIs (generic and open-type) work.
- Base-type listeners receive derived events.
- Async requests succeed/fail correctly.
- Middleware ordering and diagnostics hooks are deterministic.

## Implementation Plan Index

- Plans/Replace-EventBus/Implementations/01-hierarchy-listeners.md (Milestone 3)
- Plans/Replace-EventBus/Implementations/02-request-routing-awaitable.md (Milestone 4)
- Plans/Replace-EventBus/Implementations/03-middleware-diagnostics.md (Milestone 5)
- Plans/Replace-EventBus/Implementations/04-container-switch-migration.md (Milestone 6)
## Progress

- [x] (2026-03-09 00:00Z) Authored initial ExecPlan for replacing the current event bus implementation.
- [x] (2026-03-10 00:00Z) Updated plan direction to `Replace-EventBus`, unified on `AddListener`/`RemoveListener`, and removed standalone listener cache in favor of `Map` indexers.
- [x] (2026-03-10 00:00Z) Baseline current Events module behavior validated with focused tests and fixture coverage (generic/open-type add-remove flows and idempotence).
- [x] (2026-03-10 11:00Z) Completed Milestone 2 by adding request/middleware contracts (`ContextRequest<TResponse>`, `IRequestBus`, `IEventMiddleware`, `IRequestMiddleware`) while keeping `IEventBus` backward-compatible; updated sample/docs to explicitly show generic and open-type `AddListener`/`RemoveListener` flows; validated with `dotnet build Scaffold.sln -c Release` and focused `Scaffold.Events` project builds.
- [x] (2026-03-10 12:00Z) Completed Milestone 3 listener runtime by adding `ScalableEventBus` with `Map<Type, long, ListenerEntry>` storage, exact+hierarchy indexer dispatch, idempotent generic/open-type add/remove flows, and continue-on-failure listener invocation; added `ScalableEventBusTests` for hierarchy/idempotence/failure behavior and validated with focused builds plus analyzer workflow checks.
- [ ] Implement request routing on top of the listener core using `Awaitable`.
- [ ] Add middleware and diagnostics-ready hooks.
- [ ] Switch container wiring to new bus and retire `EventController` from active DI path.
- [ ] Finalize docs and tests across hierarchy/listener/request/middleware flows.

## Surprises & Discoveries

- Observation: Current `EventController` dispatches only exact runtime types; no inheritance traversal exists.
  Evidence: `Assets/Scripts/Infra/Events/Runtime/Implementation/EventController.cs` uses `events.TryGetValue(evt.GetType(), ...)` in `Raise`.

- Observation: `IEventBus` already exposes both generic and open-type variants using `AddListener` and `RemoveListener`, so adding `Register`/`Unregister` would duplicate API surface.
  Evidence: `Assets/Scripts/Infra/Events/Runtime/Contracts/IEventBus.cs` includes `AddListener<T>`, `RemoveListener<T>`, `AddListener(Type, ...)`, and `RemoveListener(Type, ...)`.

- Observation: `Scaffold.Maps` already provides indexed grouping that can reduce custom registry code and help future analytics aggregation.
  Evidence: `Docs/Maps.md` and `Assets/Scripts/Tools/Maps/Runtime/Map.cs`.


- Observation: Bash-based analyzer workflow cannot run in this environment because `/bin/bash` is unavailable, so workflow-equivalent checks were run with direct PowerShell `dotnet build --no-incremental` commands.
  Evidence: `bash ".../.agents/scripts/check-analyzers.sh"` failed with `CreateProcessCommon: execvpe(/bin/bash) failed: No such file or directory`.

- Observation: Expanded Events baseline tests compile cleanly with zero SCA diagnostics in `Assets/Scripts/Infra/Events/Tests/EventsTests.cs`, while unrelated pre-existing SCA warnings remain in Autopacker sample/test projects.
  Evidence: Scoped parser check returned `EVENTS_TESTS_SCA:0`; solution-level SCA output still references `Assets/Generators/Autopacker/...`.
- Observation: Milestone 1 validation was confirmed by user after baseline test expansion.
  Evidence: User message miletone 1 validated.
- Observation: Multi-line generic method signatures can trigger SCA0005 in this repository and must stay on one line even when long.
  Evidence: `Assets/Scripts/Infra/Events/Runtime/Contracts/IRequestMiddleware.cs` initially emitted `SCA0005` until signature was collapsed to one line.
- Observation: Unity batch test invocation completed with successful exit but did not produce the requested XML test result file in this environment; logs show licensing handshake errors and normal batch shutdown.
  Evidence: `Logs/Events-ScalableBus-M2.log` contains `[Licensing::Module] Error: Failed to handshake to channel: "LicenseClient-user"` plus `Batchmode quit successfully invoked - shutting down!`, while `Logs/Events-ScalableBus-M2.xml` was not created.
- Observation: The same Unity batch limitation reproduced for Milestone 3 test execution (`Events-HierarchyListeners`): command exits but no XML result artifact is produced.
  Evidence: `Logs/Events-HierarchyListeners.log` contains the same licensing handshake errors and no `Logs/Events-HierarchyListeners.xml` file exists.

## Decision Log

- Decision: Keep `IEventBus` as the backward-compatible entry contract and add new contracts/extensions for expanded capabilities.
  Rationale: This minimizes migration cost across existing modules while allowing API growth.
  Date/Author: 2026-03-09 / Codex

- Decision: Avoid per-event DI registrations by using a single bus service with runtime type routing.
  Rationale: This satisfies the explicit requirement and scales better as event types grow.
  Date/Author: 2026-03-09 / Codex

- Decision: Include middleware and diagnostics extension points in the first implementation rather than bolting them on later.
  Rationale: Dispatch pipeline shape is easiest to define correctly once, and future analytics depends on stable hooks.
  Date/Author: 2026-03-09 / Codex

- Decision: Use `Scaffold.Maps` for listener/request handler storage where it improves indexed lookup and future observability.
  Rationale: Reuses existing module primitives and reduces bespoke data-structure maintenance.
  Date/Author: 2026-03-09 / Codex

- Decision: Keep `AddListener` and `RemoveListener` as the single listener API naming across generic and open-type flows, without introducing `Register`/`Unregister` aliases.
  Rationale: The repository already uses `AddListener`/`RemoveListener`; unifying on one naming model minimizes cognitive load and migration churn.
  Date/Author: 2026-03-10 / Codex

- Decision: Resolve concrete and abstract/base dispatch using `Scaffold.Maps` indexers instead of a separate cache dictionary.
  Rationale: `Map` already provides indexer lifecycle and synchronized tracking, so a second cache risks duplication and invalidation bugs.
  Date/Author: 2026-03-10 / Codex
- Decision: Use Unity `Awaitable` for request/response async signatures instead of `ValueTask`.
  Rationale: This aligns request flow contracts with Unity-native async primitives already used in engine-facing code paths.
  Date/Author: 2026-03-10 / Codex

- Decision: Split implementation milestones so hierarchy listeners and request flow are delivered and validated independently before middleware and diagnostics.
  Rationale: This reduces integration risk and makes regressions easier to isolate during migration.
  Date/Author: 2026-03-10 / Codex


- Decision: For Milestone 1, expand baseline tests first without changing runtime behavior.
  Rationale: This creates a regression safety net before replacing `EventController` with scalable runtime implementation.
  Date/Author: 2026-03-10 / Codex
- Decision: Keep request bus open-type registration explicit on both request and response runtime types (`Type requestType, Type responseType`) while keeping strongly-typed generic registration APIs.
  Rationale: This preserves runtime registration flexibility for dynamic scenarios while keeping compile-time-safe primary usage for most callers.
  Date/Author: 2026-03-10 / Codex
- Decision: For listener dispatch in `ScalableEventBus`, maintain per-runtime-type exact and hierarchy `Map` indexers and execute listeners from a captured snapshot outside lock scope.
  Rationale: This keeps registry mutation synchronized while preventing handler execution from holding locks, and preserves deterministic exact+base dispatch behavior.
  Date/Author: 2026-03-10 / Codex
## Outcomes & Retrospective

Milestone 1 complete: baseline coverage in `Assets/Scripts/Infra/Events/Tests/EventsTests.cs` now includes generic/open-type add-remove flows and idempotence checks, and the milestone has been validated.

Milestone 2 complete: Events contracts now include request and middleware extension points without breaking `IEventBus` callers, and docs/samples now explicitly show both generic and open-type listener registration/removal paths. Build validation is green aside from pre-existing solution warnings unrelated to this milestone; Unity batch test XML output remains environment-blocked.

Milestone 3 complete: hierarchy-aware listener runtime exists in `ScalableEventBus` with tests covering base/derived dispatch, open-type idempotence, invalid type guardrails, and listener-failure isolation. Quality gate builds and analyzer checks passed for changed projects; Unity batch Events test output remains blocked by the known environment licensing handshake issue.

## Context and Orientation

The current Events module lives in `Assets/Scripts/Infra/Events/` with these key files:

- `Runtime/Contracts/ContextEvent.cs`: base type for publish/subscribe events.
- `Runtime/Contracts/IEventBus.cs`: current public event bus interface.
- `Runtime/Implementation/EventController.cs`: current in-memory implementation.
- `Container/EventsInstaller.cs`: DI registration (`IEventBus -> EventController` scoped).
- `Tests/EventsTests.cs`: existing baseline tests.

Today, subscriptions are stored by exact event type and dispatch does not traverse type hierarchies. There is no async request/response abstraction, and no middleware pipeline.

Definitions used in this plan:

- Hierarchy dispatch: publishing a concrete event also invokes handlers registered for any assignable base class or abstract class.
- Open-type API: registration/unregistration using `Type` at runtime instead of compile-time generic type parameters.
- Middleware: small components that wrap publish/request execution to add behavior such as tracing, policy checks, or metrics without modifying handlers.
- Request bus: a path where a request object expects a typed async response (for example `LoadProfileRequest -> PlayerProfile`).

## Milestone Quality Gate

Before moving from one milestone to the next, complete this gate for the current milestone:

- Run build/lint/analyzer checks (dotnet build Scaffold.sln -c Release) and fix all warnings/errors introduced by the milestone.
- Run milestone-relevant tests (at minimum Events tests; plus dependent smoke tests when the milestone affects wiring or integration).
- Fix failures and regressions, then re-run checks until green.
- Update Progress, Surprises & Discoveries, and Decision Log with what changed and what was verified.

A milestone is not considered complete until this gate passes.
## Plan of Work

### Milestone 1: Baseline and compatibility safety net

First, lock down current behavior and add migration-oriented tests before changing runtime implementation. Expand `Assets/Scripts/Infra/Events/Tests/EventsTests.cs` (or split into focused test files) to include:

- Existing listener behavior (subscribe, unsubscribe, clear).
- Idempotent duplicate add/remove scenarios.
- Compatibility assertions that generic and open-type `AddListener`/`RemoveListener` calls behave consistently.

This milestone produces a trusted regression suite so the replacement can proceed safely.

### Milestone 2: Expand contracts without breaking existing callers

Edit contracts under `Assets/Scripts/Infra/Events/Runtime/Contracts/`:

1. Keep `IEventBus` methods intact for existing consumers.
2. Keep listener API naming unified as `AddListener` and `RemoveListener` (generic and open-type). Do not introduce `Register`/`Unregister` aliases.
3. Add request abstractions:
   - `abstract record ContextRequest<TResponse>;`
   - `interface IRequestBus` with generic and open-type async registration/unregistration and `Awaitable<TResponse> RequestAsync<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken = default);`
4. Add middleware abstractions:
   - `IEventMiddleware`
   - `IRequestMiddleware`
5. Ensure docs and samples demonstrate both generic and open-type `AddListener`/`RemoveListener` flows explicitly so contributors follow one consistent path.

This milestone keeps existing call sites compiling while introducing the expanded API surface.

### Milestone 3: Implement listener runtime with hierarchy dispatch

Execution plan: Plans/Replace-EventBus/Implementations/01-hierarchy-listeners.md.

Create a new runtime implementation in `Assets/Scripts/Infra/Events/Runtime/Implementation/` (for example `ScalableEventBus.cs`) that first implements `IEventBus`.

Core implementation requirements:

- No per-event container registration.
- Generic + open-type listener registration via `AddListener`/`RemoveListener`.
- Hierarchy dispatch using assignability checks (`registeredType.IsAssignableFrom(actualType)`).
- Middleware hook points can exist as no-op placeholders at this stage, but listener publish behavior must be complete and testable.

Scalability design:

- Use `Map<Type, long, ListenerEntry>` (from `Scaffold.Maps`) for listener storage, where primary key is declared event type and secondary key is subscription id.
- Use `Map` indexers to resolve exact and hierarchy listener sets for a concrete published type (for example an exact indexer where `declaredType == actualType` and a hierarchy indexer where `declaredType.IsAssignableFrom(actualType)` and `declaredType != actualType`).
- Do not maintain a standalone listener cache dictionary; rely on `Map` and its indexer registry as the single source of truth.
- Keep lock scope limited to registry and map/indexer mutation; invoke handlers outside locks.
- Ensure clear error behavior on publish: continue other listeners when one fails, and report failure to diagnostics.

### Milestone 4: Implement request routing using `Awaitable`

Execution plan: Plans/Replace-EventBus/Implementations/02-request-routing-awaitable.md.

Extend `ScalableEventBus` (or a tightly coupled companion class in the same module) to implement `IRequestBus` after hierarchy listeners are already working.

Core implementation requirements:

- Store request handlers in `Map<Type, long, RequestHandlerEntry>` with the same registration/unregistration guarantees used for listeners.
- Route requests by exact request runtime type unless an explicit hierarchy policy is added and documented.
- `RequestAsync` must return `Awaitable<TResponse>` and support cancellation.
- Request error behavior must be deterministic:
  - No handler: fail the returned `Awaitable` with a clear exception.
  - Handler failure: fail the returned `Awaitable` and report through diagnostics hooks.

Acceptance focus for this milestone:

- Request success path works with typed response.
- No-handler and handler-throws paths are both covered by tests and produce expected failures.

### Milestone 5: Diagnostics and analytics-ready extension points

Execution plan: Plans/Replace-EventBus/Implementations/03-middleware-diagnostics.md.

Add new contracts in `Runtime/Contracts/`:

- `IEventDiagnosticsSink` with methods such as:
  - `OnEventPublished(Type eventType, int listenerCount)`
  - `OnListenerInvoked(Type eventType, Type declaredType, double durationMs)`
  - `OnListenerFailed(Type eventType, Exception exception)`
  - `OnRequestCompleted(Type requestType, bool success, double durationMs)`
- `EventDispatchContext` lightweight struct/record (timestamp, correlation id, event type/request type).

Runtime behavior:

- Default to no-op sink when diagnostics is not configured.
- Keep instrumentation cheap (minimal allocations, reuse metadata where safe).
- Expose middleware hooks with deterministic ordering and documented execution model.

### Milestone 6: Container switch and migration completion

Execution plan: Plans/Replace-EventBus/Implementations/04-container-switch-migration.md.

Edit `Assets/Scripts/Infra/Events/Container/EventsInstaller.cs`:

- Register new implementation as the scoped service for `IEventBus`.
- Register `IRequestBus` to the same scoped instance.
- Register diagnostics sink default implementation (no-op) and middleware collection support.

Migration completion:

- Update `Assets/Scripts/Infra/Events/Samples/EventsUseCases.cs` to show both old and new API usage.
- Mark `EventController` obsolete first, then remove from active DI path.
- Keep backward compatibility methods available so existing modules do not need immediate changes.

### Milestone 7: Documentation and final validation

Update `Docs/Events.md` to describe:

- New contracts (`IRequestBus`, middleware, diagnostics sink).
- Hierarchy dispatch semantics.
- Async request lifecycle.
- Middleware order and error behavior.
- Migration guidance for `AddListener` users.

Also create or update tests in `Assets/Scripts/Infra/Events/Tests/` for:

- Hierarchy dispatch (abstract/base handlers receiving derived events).
- Open-type `AddListener`/`RemoveListener`.
- Async request success and failure.
- Middleware wrapping order.
- Diagnostics hook invocation.
- Backward compatibility with existing `IEventBus` methods.

## Concrete Steps

Run all commands from repository root: `C:/Users/user/Documents/Unity/Scaffold`.

1. Inspect baseline Events files before edits.

    Get-Content Assets/Scripts/Infra/Events/Runtime/Contracts/IEventBus.cs
    Get-Content Assets/Scripts/Infra/Events/Runtime/Implementation/EventController.cs
    Get-Content Assets/Scripts/Infra/Events/Container/EventsInstaller.cs
    Get-Content Assets/Scripts/Infra/Events/Tests/EventsTests.cs

2. Implement milestones in order: contracts/extensions in this parent plan, then execute child plans for Milestones 3-6 (`01-hierarchy-listeners.md`, `02-request-routing-awaitable.md`, `03-middleware-diagnostics.md`, `04-container-switch-migration.md`), then finalize tests/docs.

3. Build solution-level C# projects to catch compile issues early.

    dotnet build Scaffold.sln -c Release

4. Run Events-focused tests via Unity CLI.

    Unity.exe -batchmode -quit -projectPath "C:\Users\user\Documents\Unity\Scaffold" -runTests -testPlatform EditMode -testFilter "Scaffold.Events.Tests" -testResults "Logs\Events-ScalableBus.xml"

5. Run at least one dependent module smoke test (Navigation) to confirm compatibility.

    Unity.exe -batchmode -quit -projectPath "C:\Users\user\Documents\Unity\Scaffold" -runTests -testPlatform EditMode -testFilter "Scaffold.Navigation.Tests" -testResults "Logs\Navigation-AfterEventsBusReplace.xml"

Expected indicators of success:

- Build succeeds with no analyzer violations.
- Events tests pass including new hierarchy/request/middleware cases.
- Navigation tests pass without modifying their event-consumer semantics.

## Validation and Acceptance

This work is accepted only when all conditions are true:

1. `EventsInstaller` resolves `IEventBus` to the new scalable implementation; no per-event DI registration exists.
2. API supports `AddListener`/`RemoveListener`/`Raise` in generic and open-type forms.
3. Existing `AddListener`/`RemoveListener` consumers continue working unchanged.
4. Hierarchy dispatch is implemented and proven by tests.
5. Async requests are implemented with success and failure path tests.
6. Middleware exists, is ordered deterministically, and is covered by tests.
7. Diagnostics sink extension points exist and are tested for invocation.
8. `Docs/Events.md` is updated to reflect the new architecture and migration guidance.
9. Analyzer warnings/errors introduced by this change are fixed before merge.

## Idempotence and Recovery

This migration is designed to be safe and incremental:

- Milestones 1-5 are additive and can be rerun without data loss.
- If replacement runtime fails during Milestone 6, temporarily revert only `EventsInstaller` binding to `EventController` while keeping new contracts/tests in place.
- Re-running `AddListener`/`RemoveListener` tests must remain deterministic and prove idempotence.
- Avoid deleting legacy files until replacement tests are stable and passing.

## Artifacts and Notes

Capture concise evidence snippets while implementing:

- Test output proving hierarchy dispatch behavior.
- Test output proving request success and no-handler failure behavior.
- A middleware order trace (for example `before-A`, `before-B`, `after-B`, `after-A`).
- A diagnostics invocation trace showing publish and listener-failure hooks.

Keep artifacts short and place any larger logs under `Logs/`.

## Interfaces and Dependencies

Interfaces expected at completion:

- Existing: `Scaffold.Events.IEventBus` (unchanged for backward compatibility).
- New/expanded:
  - `Scaffold.Events.ContextRequest<TResponse>`.
  - `Scaffold.Events.IRequestBus`.
  - `Scaffold.Events.IEventMiddleware`.
  - `Scaffold.Events.IRequestMiddleware`.
  - `Scaffold.Events.IEventDiagnosticsSink`.

Concrete implementation expected at completion:

- `Scaffold.Events.ScalableEventBus` (or equivalent final class name) in `Runtime/Implementation`.

Dependencies:

- `Scaffold.Maps` should be used when it provides simpler indexed registries and future analytics-friendly grouping.
- Existing `Scaffold.Containers` abstractions must remain the only DI integration surface in `EventsInstaller`.
- No new dependency may require event-by-event registration boilerplate.

---

Revision Note (2026-03-09): Initial plan created for replacing `IEventBus` implementation with a scalable, hierarchy-aware, middleware-enabled, async-capable event bus while preserving compatibility and enabling future diagnostics/analytics.
Revision Note (2026-03-10): Renamed plan to `Replace-EventBus`, standardized listener API naming on `AddListener`/`RemoveListener`, and removed standalone listener cache dictionary in favor of `Scaffold.Maps` indexer-driven concrete and hierarchy dispatch.
Revision Note (2026-03-10): Replaced `ValueTask` request signatures with Unity `Awaitable` and split runtime implementation milestones into listener hierarchy dispatch, then request routing, then middleware/diagnostics.
Revision Note (2026-03-10): Clarified that Milestones 3-6 are executed through child plans in Plans/Replace-EventBus/Implementations/ and updated concrete execution order accordingly.
Revision Note (2026-03-10): Added explicit per-milestone quality gate requiring build/lint/analyzer checks, tests, fixes, and living-section updates before proceeding.
Revision Note (2026-03-10): Implemented Milestone 1 test expansion, documented environment limitations for workflow/test execution, and recorded remaining verification step for Unity EditMode output confirmation.
Revision Note (2026-03-10): Milestone 1 marked validated based on user confirmation after baseline test expansion.
Revision Note (2026-03-10): Updated milestone flow to require a commit immediately after validation/testing gate passes.
Revision Note (2026-03-10): Completed Milestone 2 contract expansion and compatibility documentation/sample updates, and recorded Unity batch test XML generation limitation observed during milestone gate execution.
Revision Note (2026-03-10): Completed Milestone 3 scalable hierarchy listener runtime and tests, and recorded repeated Unity batch test XML output limitation in this environment.

