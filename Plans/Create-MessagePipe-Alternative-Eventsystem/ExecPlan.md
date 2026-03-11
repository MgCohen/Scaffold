# Replace Events Module with a MessagePipe-Backed Event System

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this work, the current `EventController` implementation will be replaced by a MessagePipe-backed event system that preserves familiar ergonomics while adding hierarchy dispatch, async request/response, and middleware. Contributors will continue to publish and subscribe to events in one place, but the runtime will scale better under growth and provide explicit extension points for diagnostics and analytics.

The observable result is that modules using `IEventBus` continue to work without per-event container registration, while new tests prove abstract/base event subscriptions, async requests, and middleware behavior. The replacement is complete when `EventsInstaller` resolves `IEventBus` to the new implementation and legacy `EventController` is removed from runtime wiring.

## Progress

- [x] (2026-03-09 21:55Z) Authored replacement-focused ExecPlan with architecture, migration, and acceptance criteria.
- [ ] Add MessagePipe dependency set and verify Unity/VContainer compatibility in this repository.
- [ ] Introduce replacement contracts and compatibility extensions.
- [ ] Implement MessagePipe-backed runtime and switch installers to it.
- [ ] Remove legacy runtime implementation from active DI path and update docs.
- [ ] Add and pass comprehensive replacement tests, including compatibility with existing consumers.

## Surprises & Discoveries

- Observation: The repository already depends on VContainer and wraps it through `IContainerRegistry`, so DI integration must be implemented via the wrapper layer and installer conventions.
  Evidence: `Packages/manifest.json`, `Assets/Scripts/Infra/Containers/Runtime/Implementation/VContainerRegistry.cs`.

- Observation: Current event dispatch only targets exact concrete event types, so hierarchy support is a behavioral expansion that must be explicitly tested.
  Evidence: `Assets/Scripts/Infra/Events/Runtime/Implementation/EventController.cs` (`Raise` uses concrete type lookup).

- Observation: `Scaffold.Maps` already supports indexed grouped storage and is suitable for listener registries plus future analytics counters/indexers.
  Evidence: `Assets/Scripts/Tools/Maps/Runtime/Map.cs`, `Assets/Scripts/Tools/Maps/Runtime/Indexer.cs`.

## Decision Log

- Decision: This is a replacement project, not a long-term dual-bus architecture.
  Rationale: User asked to replace current implementation. Temporary adapters are allowed only during migration milestones.
  Date/Author: 2026-03-09 / Codex

- Decision: Keep `IEventBus` as the stable consumer contract and expand through additional interfaces and extension methods instead of forcing immediate consumer-wide signature changes.
  Rationale: Existing modules (for example Navigation) already depend on `IEventBus`; replacement should avoid churn while unlocking new capabilities.
  Date/Author: 2026-03-09 / Codex

- Decision: Use a single MessagePipe channel with envelope dispatch plus in-process type resolution to avoid per-event registration.
  Rationale: Satisfies requirement 1 and keeps startup cost bounded as event types grow.
  Date/Author: 2026-03-09 / Codex

- Decision: Use `Scaffold.Maps` for subscription storage keyed by `(declaredEventType, subscriptionId)`.
  Rationale: It reduces custom bookkeeping code and creates convenient index points for diagnostics/analytics later.
  Date/Author: 2026-03-09 / Codex

## Outcomes & Retrospective

Not started yet. Update this section at each major milestone with achieved outcomes, gaps, and lessons.

## Context and Orientation

The current module at `Assets/Scripts/Infra/Events/` provides `ContextEvent`, `IEventBus`, and `EventController`. `EventsInstaller` currently registers `IEventBus -> EventController`. Consumers such as Navigation publish lifecycle events through this contract.

This plan replaces `EventController` runtime behavior with a MessagePipe-backed implementation while preserving call-site compatibility. “Open type” means subscribe/unsubscribe by `Type` and `Action<ContextEvent>`. “Hierarchy events” means handlers registered for abstract/base types are invoked for matching derived concrete events. “Middleware” means composable interceptors that wrap dispatch/request execution for policy, tracing, and analytics.

## Plan of Work

### Milestone 1: Dependency and installer readiness

Add MessagePipe packages compatible with this Unity/VContainer stack. Confirm assemblies are available and can be referenced from the Events runtime assembly. Create or update installer wiring so MessagePipe infrastructure is registered once at container composition time, with no per-event type registration.

At the end of this milestone, the repository compiles with MessagePipe available and installer scaffolding prepared for runtime replacement.

### Milestone 2: Contract expansion with compatibility

Keep `IEventBus` intact for existing modules. Add replacement-facing contracts under `Assets/Scripts/Infra/Events/Runtime/Contracts/`:

- `IEventBusEx` for expanded event APIs:
  - `Register<TEvent>(Action<TEvent>)`
  - `Unregister<TEvent>(Action<TEvent>)`
  - `Register(Type, Action<ContextEvent>)`
  - `Unregister(Type, Action<ContextEvent>)`
  - `Raise(ContextEvent)`
  - `Clear()`

- `IRequestBus` for async request-response:
  - typed register/unregister handler methods
  - typed `RequestAsync`
  - open-type request method for runtime scenarios

- `ContextRequest<TResponse>` base record.

- Middleware contracts:
  - `IEventMiddleware`
  - `IRequestMiddleware`

Add extension methods so `IEventBus` consumers can call `Register`/`Unregister` even when they only hold `IEventBus`, and map existing `AddListener`/`RemoveListener` semantics to the new naming. This preserves familiar API while allowing incremental adoption of expanded interfaces.

### Milestone 3: Implement replacement runtime

Create `MessagePipeEventController` in `Assets/Scripts/Infra/Events/Runtime/Implementation/` implementing `IEventBus`, `IEventBusEx`, and `IRequestBus`.

Implementation requirements for scalability and efficiency:

- Publish all events through one envelope topic (payload + runtime type metadata).
- Resolve dispatch targets using assignable-type matching (`registeredType.IsAssignableFrom(actualType)`) so hierarchy listeners work.
- Use `Map<Type, long, SubscriptionEntry>` for listener registry.
- Maintain cached resolved listener arrays per concrete event type for fast repeated dispatch; invalidate on register/unregister/clear.
- Execute middleware as a deterministic pipeline around dispatch and request handling.
- Keep callback invocation outside locks; lock only mutable registry/cache state.
- Event exceptions: isolate per listener and continue dispatch; route failures into diagnostics hook.
- Request exceptions: fail the returned `ValueTask`.

### Milestone 4: Switch active implementation and retire legacy path

Update `Assets/Scripts/Infra/Events/Container/EventsInstaller.cs` to register `IEventBus` to `MessagePipeEventController` (and register `IEventBusEx`/`IRequestBus` to same scoped instance).

Then retire legacy runtime path:

- Mark `EventController` obsolete in one intermediate commit if needed for migration safety.
- After all tests pass using replacement wiring, remove `EventController` from active usage and samples.
- Keep compatibility extensions so old call patterns compile and behave identically where expected.

At the end of this milestone, replacement is complete: `EventsInstaller` resolves only the new runtime bus.

### Milestone 5: Diagnostics/analytics readiness and docs

Add explicit extension points to `MessagePipeEventController`:

- `IEventDiagnosticsSink` interface with hooks such as `OnPublish`, `OnDispatchStart`, `OnDispatchEnd`, `OnListenerError`, and `OnRequestCompleted`.
- Optional lightweight event metadata context (timestamp, correlation id, event type, handler count, duration).

These hooks must default to no-op and add near-zero overhead when not configured.

Update `Docs/Infra/Events.md` to document replacement architecture, migration notes for existing API usage, and diagnostics hook integration.

## Concrete Steps

Run from repository root `C:/Users/user/Documents/Unity/Scaffold`.

1. Confirm current events and container baseline.

    Get-Content -Raw Assets/Scripts/Infra/Events/Runtime/Contracts/IEventBus.cs
    Get-Content -Raw Assets/Scripts/Infra/Events/Runtime/Implementation/EventController.cs
    Get-Content -Raw Assets/Scripts/Infra/Events/Container/EventsInstaller.cs

2. Add MessagePipe package dependencies using repository-standard Unity workflow and reload project.

3. Verify MessagePipe availability.

    Get-ChildItem -Recurse -Path Library/PackageCache,Packages -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match "MessagePipe" }

4. Implement contracts, runtime, installer switch, and tests in milestone order.

5. Run Events tests.

    Unity.exe -batchmode -quit -projectPath "C:\Users\user\Documents\Unity\Scaffold" -runTests -testPlatform EditMode -testFilter "Scaffold.Events.Tests" -testResults "Logs\Events-Replacement.xml"

6. Run Navigation smoke tests to confirm `IEventBus` compatibility.

    Unity.exe -batchmode -quit -projectPath "C:\Users\user\Documents\Unity\Scaffold" -runTests -testPlatform EditMode -testFilter "Scaffold.Navigation.Tests" -testResults "Logs\Navigation-AfterEventsReplace.xml"

## Validation and Acceptance

Replacement is accepted only when all conditions hold:

1. `EventsInstaller` resolves `IEventBus` to MessagePipe-backed `MessagePipeEventController`.
2. No per-event registration is required in DI; newly added event types work without installer edits.
3. Generic and open-type `Register`/`Unregister`/`Raise` APIs are available and covered by tests.
4. Abstract/base subscriptions receive derived event publications.
5. Async requests work with success and failure paths validated.
6. Middleware order and short-circuit behavior are deterministic and tested.
7. Existing consumers still using `IEventBus.AddListener/RemoveListener` continue to pass tests through compatibility layer.
8. Diagnostics sink hooks exist, are documented, and have test coverage for invocation.
9. `Docs/Infra/Events.md` reflects replacement architecture and migration guidance.

## Idempotence and Recovery

All changes are additive until Milestone 4 switch-over. During migration, if replacement wiring fails, temporarily rebind `EventsInstaller` to legacy `EventController` and continue fixing in a feature branch. Because compatibility extensions are retained, call sites do not need immediate rollback edits.

For repeated runs, register/unregister/clear operations must remain idempotent with stable cache invalidation. Tests for duplicate registration and repeated unregister calls are mandatory to prevent drift.

## Artifacts and Notes

Capture concise implementation evidence during execution:

- Dependency resolution output showing MessagePipe assemblies detected.
- Test output snippets proving hierarchy dispatch and async request paths.
- One snippet showing middleware wrapping order (`before A`, `before B`, `after B`, `after A`).
- One snippet showing diagnostics sink receiving publish/error callbacks.

## Interfaces and Dependencies

Required interfaces/types at completion:

- Existing: `Scaffold.Events.IEventBus` remains.
- New: `Scaffold.Events.IEventBusEx`, `Scaffold.Events.IRequestBus`, `Scaffold.Events.ContextRequest<TResponse>`, `Scaffold.Events.IEventMiddleware`, `Scaffold.Events.IRequestMiddleware`, `Scaffold.Events.IEventDiagnosticsSink`.
- New runtime: `Scaffold.Events.MessagePipeEventController`.
- Installer target: `Scaffold.Events.Container.EventsInstaller` updated to replacement runtime.

Dependencies:

- MessagePipe packages compatible with this project’s Unity and VContainer versions.
- Existing `Scaffold.Maps` for listener registry indexing.
- Existing `Scaffold.Containers` abstractions for all registration code.

No design in this plan is allowed to require one container registration per event type.

---

Revision Note (2026-03-09): Reworked the prior alternative-bus plan into a replacement plan that ends with `EventsInstaller` switched to MessagePipe-backed runtime and legacy implementation retired from active wiring.