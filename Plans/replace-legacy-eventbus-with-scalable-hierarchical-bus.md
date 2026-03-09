# Replace Legacy Event Bus with a Scalable Hierarchical Bus

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this work, Scaffold will have a replacement for `Assets/Scripts/Infra/Events/Runtime/Contracts/IEventBus.cs` that keeps the familiar listener workflow while removing the need for per-event container registrations. The new bus will support both exact and hierarchy dispatch (base and abstract handlers receive derived events), async request/response flows, and middleware hooks for cross-cutting concerns.

A contributor will be able to verify success by running Events tests that prove:

- Existing `AddListener`/`RemoveListener`/`Raise` flows still work.
- New `Register`/`Unregister` APIs (generic and open-type) work.
- Base-type listeners receive derived events.
- Async requests succeed/fail correctly.
- Middleware ordering and diagnostics hooks are deterministic.

## Progress

- [x] (2026-03-09 00:00Z) Authored initial ExecPlan for replacing the current event bus implementation.
- [ ] Baseline current Events module behavior with focused tests and fixture coverage.
- [ ] Introduce expanded contracts and compatibility extensions.
- [ ] Implement scalable runtime bus with hierarchy dispatch, async requests, and middleware.
- [ ] Switch container wiring to new bus and retire `EventController` from active DI path.
- [ ] Add diagnostics-ready hooks and finalize docs/tests.

## Surprises & Discoveries

- Observation: Current `EventController` dispatches only exact runtime types; no inheritance traversal exists.
  Evidence: `Assets/Scripts/Infra/Events/Runtime/Implementation/EventController.cs` uses `events.TryGetValue(evt.GetType(), ...)` in `Raise`.

- Observation: Current API names are `AddListener` and `RemoveListener`, while the requested target API names are `Register` and `Unregister`.
  Evidence: `Assets/Scripts/Infra/Events/Runtime/Contracts/IEventBus.cs`.

- Observation: `Scaffold.Maps` already provides indexed grouping that can reduce custom registry code and help future analytics aggregation.
  Evidence: `Docs/Maps.md` and `Assets/Scripts/Tools/Maps/Runtime/Map.cs`.

## Decision Log

- Decision: Keep `IEventBus` as the backward-compatible entry contract and add new contracts/extensions for expanded capabilities.
  Rationale: This minimizes migration cost across existing modules while allowing API growth.
  Date/Author: 2026-03-09 / Codex

- Decision: Avoid per-event DI registrations by using a single bus service with runtime type routing and cache invalidation.
  Rationale: This satisfies the explicit requirement and scales better as event types grow.
  Date/Author: 2026-03-09 / Codex

- Decision: Include middleware and diagnostics extension points in the first implementation rather than bolting them on later.
  Rationale: Dispatch pipeline shape is easiest to define correctly once, and future analytics depends on stable hooks.
  Date/Author: 2026-03-09 / Codex

- Decision: Use `Scaffold.Maps` for listener/request handler storage where it improves indexed lookup and future observability.
  Rationale: Reuses existing module primitives and reduces bespoke data-structure maintenance.
  Date/Author: 2026-03-09 / Codex

## Outcomes & Retrospective

Not started yet. Update this section at each milestone completion with achieved behaviors, remaining gaps, and lessons learned.

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

## Plan of Work

### Milestone 1: Baseline and compatibility safety net

First, lock down current behavior and add migration-oriented tests before changing runtime implementation. Expand `Assets/Scripts/Infra/Events/Tests/EventsTests.cs` (or split into focused test files) to include:

- Existing listener behavior (subscribe, unsubscribe, clear).
- Idempotent duplicate register/unregister scenarios.
- Compatibility assertions that future `Register` wrappers behave like `AddListener`.

This milestone produces a trusted regression suite so the replacement can proceed safely.

### Milestone 2: Expand contracts without breaking existing callers

Edit contracts under `Assets/Scripts/Infra/Events/Runtime/Contracts/`:

1. Keep `IEventBus` methods intact for existing consumers.
2. Add a new interface `IEventBusV2` (name may be `IEventBusEx` if team prefers) with:
   - `void Register<TEvent>(Action<TEvent> handler) where TEvent : ContextEvent;`
   - `void Unregister<TEvent>(Action<TEvent> handler) where TEvent : ContextEvent;`
   - `void Register(Type eventType, Action<ContextEvent> handler);`
   - `void Unregister(Type eventType, Action<ContextEvent> handler);`
   - `void Raise(ContextEvent evt);`
   - `void Clear();`
3. Add request abstractions:
   - `abstract record ContextRequest<TResponse>;`
   - `interface IRequestBus` with generic and open-type async registration/unregistration and `ValueTask<TResponse> RequestAsync<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken = default);`
4. Add middleware abstractions:
   - `IEventMiddleware`
   - `IRequestMiddleware`
5. Add extension methods in a new file (for example `IEventBusExtensions.cs`) that map:
   - `AddListener` -> `Register`
   - `RemoveListener` -> `Unregister`
   This preserves familiar API names while exposing requested naming.

This milestone keeps existing call sites compiling while introducing the expanded API surface.

### Milestone 3: Implement scalable replacement runtime

Create a new runtime implementation in `Assets/Scripts/Infra/Events/Runtime/Implementation/` (for example `ScalableEventBus.cs`) that implements `IEventBus`, `IEventBusV2`, and `IRequestBus`.

Core implementation requirements:

- No per-event container registration.
- Generic + open-type listener registration.
- Hierarchy dispatch using assignability checks (`registeredType.IsAssignableFrom(actualType)`).
- Async request routing with strongly typed response.
- Middleware pipelines around publish and request paths.

Scalability design:

- Use `Map<Type, long, ListenerEntry>` (from `Scaffold.Maps`) for listener storage, where primary key is declared event type and secondary key is subscription id.
- Use `Map<Type, long, RequestHandlerEntry>` for request handlers.
- Maintain cache `Dictionary<Type, ListenerEntry[]>` that stores resolved listeners for a concrete published type; invalidate cache on register/unregister/clear.
- Keep lock scope limited to registry and cache mutation; invoke handlers outside locks.
- Ensure clear error behavior:
  - Publish path: continue other listeners when one fails; report failure to diagnostics sink.
  - Request path: fail returned `ValueTask` when no handler exists or handler throws.

### Milestone 4: Diagnostics and analytics-ready extension points

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

### Milestone 5: Container switch and migration completion

Edit `Assets/Scripts/Infra/Events/Container/EventsInstaller.cs`:

- Register new implementation as the scoped service for `IEventBus`.
- Register `IEventBusV2` and `IRequestBus` to the same scoped instance.
- Register diagnostics sink default implementation (no-op) and middleware collection support.

Migration completion:

- Update `Assets/Scripts/Infra/Events/Samples/EventsUseCases.cs` to show both old and new API usage.
- Mark `EventController` obsolete first, then remove from active DI path.
- Keep backward compatibility methods available so existing modules do not need immediate changes.

### Milestone 6: Documentation and final validation

Update `Docs/Events.md` to describe:

- New contracts (`IEventBusV2`, `IRequestBus`, middleware, diagnostics sink).
- Hierarchy dispatch semantics.
- Async request lifecycle.
- Middleware order and error behavior.
- Migration guidance for `AddListener` users.

Also create or update tests in `Assets/Scripts/Infra/Events/Tests/` for:

- Hierarchy dispatch (abstract/base handlers receiving derived events).
- Open-type register/unregister.
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

2. Implement milestones in order: contracts/extensions, runtime, installer, tests, docs.

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
2. API supports `Register`/`Unregister`/`Raise` in generic and open-type forms.
3. Existing `AddListener`/`RemoveListener` consumers continue working unchanged.
4. Hierarchy dispatch is implemented and proven by tests.
5. Async requests are implemented with success and failure path tests.
6. Middleware exists, is ordered deterministically, and is covered by tests.
7. Diagnostics sink extension points exist and are tested for invocation.
8. `Docs/Events.md` is updated to reflect the new architecture and migration guidance.
9. Analyzer warnings/errors introduced by this change are fixed before merge.

## Idempotence and Recovery

This migration is designed to be safe and incremental:

- Milestones 1-4 are additive and can be rerun without data loss.
- If replacement runtime fails during Milestone 5, temporarily revert only `EventsInstaller` binding to `EventController` while keeping new contracts/tests in place.
- Re-running register/unregister tests must remain deterministic and prove idempotence.
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
  - `Scaffold.Events.IEventBusV2` (or `IEventBusEx`, pick one stable name and use consistently).
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
