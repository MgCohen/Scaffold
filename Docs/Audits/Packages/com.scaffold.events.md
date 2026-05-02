# com.scaffold.events — Audit

## Summary
Hand-rolled type-keyed pub/sub bus over `Action<ContextEvent>` with a VContainer installer. Functional, but the implementation duplicates state-validity guards everywhere, leaks `UnityEngine` for no reason, and reinvents a wheel that MessagePipe / R3 already turn fast and allocation-aware. **Verdict: refactor (or replace with MessagePipe and keep `IEventBus` as a façade).**

## Structure
```
com.scaffold.events/
  Container/
    EventsInstaller.cs                       (VContainer)
    Scaffold.Events.Container.asmdef
  Runtime/
    Contracts/
      ContextEvent.cs                        (abstract record)
      IEventBus.cs
    Implementation/EventController.cs        (uses UnityEngine import)
    Scaffold.Events.asmdef                   (references com.scaffold.types?? — GUID b967d1...)
  Samples/EventsUseCases.cs
  Tests/Scaffold.Events.Tests.asmdef         (asmdef only — no tests)
  package.json, README.md
```

## What's good
- `ContextEvent` as an `abstract record` (`Runtime/Contracts/ContextEvent.cs:3`) is exactly right — value semantics, init-only properties, free `with`/equality.
- `IEventBus` lives in `Contracts/`, impl in `Implementation/`, installer in `Container/`. Clean MVP layering.
- Generic + non-generic `AddListener`/`RemoveListener` (`Runtime/Contracts/IEventBus.cs:7-10`) covers reflection-driven subscribers.
- The wrapper-cache pattern in `EventController.AddListener<T>` (`Runtime/Implementation/EventController.cs:20-22`) keeps `Action<T> → Action<ContextEvent>` stable so unsubscription works.

## Issues / smells

### Redundant guard clauses on readonly fields (rubric hot button)
- `Runtime/Implementation/EventController.cs:10-11` — `events` and `eventLookups` are `readonly` and initialized inline. They cannot be null.
- `:27-28`, `:75-76`, `:92-95` — `if (events == null) throw …` and `if (eventLookups == null) throw …` repeated in `AddListener`, `Raise`, `ValidateState`, called from `Clear()`. **All unreachable.** Delete every one.
- `ValidateState()` (`:92-96`) is dead code: it can only ever return without throwing.
- The non-generic `AddListener` (`:25-32`) has four guards. The only meaningful ones are `type` and `newAction` null-checks — and those are entry-point checks for an internal/non-generic overload that `AddListener<T>` already calls. The generic path bypasses them; the non-generic path duplicates them. Pick one entry point.

### Default-values masking errors
- `Runtime/Implementation/EventController.cs:15-18` — silent return when the same handler is added twice. Either treat double-subscribe as a programmer error (throw) or document idempotency. Right now you get a coin flip.
- `:36-39` — silent `return` when removing an unknown handler. Same issue. Fail-fast preferred per rubric.
- `:46-50` — same in non-generic `RemoveListener`.
- `Raise` swallows "no listeners" silently (`:79-82`). That one is fine and standard for a bus.

### Unity / C# boundary
- `Runtime/Implementation/EventController.cs:3` — `using UnityEngine;` is unused. Delete; the class is pure C#.
- Asmdef has `noEngineReferences: false` (`Runtime/Scaffold.Events.asmdef:15`). Set it to `true` once the import is gone — this is a domain-layer eventbus.
- The asmdef references GUID `b967d18ed9aae19488f12b55c25800dd` — one assembly. Confirm this is `Scaffold.Types`; if not used, drop it. The runtime code doesn't import `Scaffold.Types`.

### Missing generics / weak typing
- `IEventBus.AddListener(Type, Action<ContextEvent>)` (`Runtime/Contracts/IEventBus.cs:9-10`) is the reflection door. It's necessary, but it should be on a separate `IDynamicEventBus` so 99% of call sites get the typed surface only.
- No `IEventBus.Raise<T>(T evt) where T : ContextEvent` overload — every raise costs a `GetType()` reflection lookup at `EventController.cs:78`. The typed overload can dispatch to a `Dictionary<Type, Action<T>>` per-T using a static generic class trick (a la MessagePipe / Zenject SignalBus). Significant perf win, zero call-site change.
- `IEventBus.Clear()` is a foot-gun on a singleton — clears everyone's subscriptions. No scoping. Consider per-scope buses via VContainer scoping or remove `Clear` from the public surface.

### Over/under-abstraction
- `IEventBus` is justified (one impl now, but consumers shouldn't depend on the controller class).
- `EventController` is `public` and concrete-instantiable in `Samples/EventsUseCases.cs:11, 21`. Either seal it and hide it (`internal`), or accept that consumers can bypass DI. The samples imply the latter, which fights the installer.
- `UpdateOrRemoveAction` and `ApplyOrRemoveAction` (`:55-71`) are split for no reason — both private, one calls the other, single use site. Inline.

### Tests
- `Tests/Scaffold.Events.Tests.asmdef` exists but no `.cs` files. Same problem as types — empty harness. The bus has nontrivial behavior (double-subscribe, unknown-unsubscribe, type dispatch); priority for regression coverage.

## Suggested before/after

**Kill the unreachable guards.**
```csharp
// before — Runtime/Implementation/EventController.cs
public void AddListener(Type type, Action<ContextEvent> newAction)
{
    if (events == null) throw new InvalidOperationException("Event registry was not initialized.");
    if (eventLookups == null) throw new InvalidOperationException("Event lookup registry was not initialized.");
    if (type == null) throw new ArgumentNullException(nameof(type));
    if (newAction == null) throw new ArgumentNullException(nameof(newAction));
    events[type] = events.TryGetValue(type, out var current) ? current + newAction : newAction;
}

// after
public void AddListener(Type type, Action<ContextEvent> handler)
{
    ArgumentNullException.ThrowIfNull(type);
    ArgumentNullException.ThrowIfNull(handler);
    events[type] = events.TryGetValue(type, out var current) ? current + handler : handler;
}
```

**Typed `Raise<T>` to skip per-call reflection.**
```csharp
public void Raise<T>(T evt) where T : ContextEvent
{
    ArgumentNullException.ThrowIfNull(evt);
    if (events.TryGetValue(typeof(T), out var action)) action.Invoke(evt);
}
```

**Make double-subscribe fail-fast.**
```csharp
public void AddListener<T>(Action<T> handler) where T : ContextEvent
{
    if (eventLookups.ContainsKey(handler))
        throw new InvalidOperationException($"Handler already subscribed for {typeof(T).Name}.");
    Action<ContextEvent> wrapper = e => handler((T)e);
    eventLookups[handler] = wrapper;
    AddListener(typeof(T), wrapper);
}
```

## Easy wins
1. Delete `using UnityEngine;` (`Runtime/Implementation/EventController.cs:3`) and flip asmdef `noEngineReferences: true`.
2. Delete every `events == null` / `eventLookups == null` guard and the `ValidateState()` method (`:27-28, 75-76, 87, 92-96`).
3. Inline `UpdateOrRemoveAction` + `ApplyOrRemoveAction` (`:55-71`) into `RemoveListener`.
4. Add a typed `Raise<T>` overload to `IEventBus` and `EventController` — saves a `GetType()` per raise.
5. Add a single `EventControllerTests.cs` covering: subscribe, raise, double-subscribe, double-unsubscribe, unknown-unsubscribe, clear.

## Organization & docs
- README exists. Confirm it documents the double-subscribe semantics (currently silent no-op) and `Clear()` scope (global to bus instance).
- Naming: `EventController` reads like a Unity controller — `EventBus` would be the conventional name for an `IEventBus` impl.
- Consider a `Channels<T>` style pull-based subscription using `IObservable<T>`/`R3` for view-models — fits MVVM project-wide and removes the need to manage handler dictionaries by hand. Reference: MessagePipe (Cysharp) for VContainer-friendly typed pub/sub; R3 for observable streams.
- `Tests/` asmdef without .cs files: add or remove.

## Consumers

Single consumer package: `com.scaffold.navigation` (12 runtime files + 1 test fake). No other Scaffold package raises or subscribes to `ContextEvent` in `Assets/`. No `Assets/Scaffold/` consumer.

**`com.scaffold.navigation/Runtime/Implementation/NavigationTransitions.cs:89, 96, 102, 104`** — every raise is "construct then raise":
```csharp
var beforeOpenEvent = new BeforeViewOpenEvent(viewType); events.Raise(beforeOpenEvent); to.View.gameObject.SetActive(true);
...
var afterOpenEvent = new AfterViewOpenEvent(viewType); events.Raise(afterOpenEvent);
```
Smells: (1) `Raise(ContextEvent)` boxes/dispatches via runtime `GetType()` per call (audit's `Implementation/EventController.cs:78` finding) — typed `Raise<T>` would devirtualize. (2) Multiple statements per line obscure that 4 events fire in one transition; profile this hot path. (3) Allocates a fresh record per raise even for record types with no payload variance (`AfterViewOpenEvent(viewType)` could be cached per `viewType` if the bus accepted struct events).

**`com.scaffold.navigation/Runtime/Implementation/NavigationController.cs:15`** — every controller takes `IEventBus` via ctor:
```csharp
public NavigationController(IEventBus events, NavigationSettings settings, Transform viewHolder, IEnumerable<INavigationMiddleware> middlewares, IAddressablesGateway addressablesGateway, IViewControllerDependencyInjector dependencyInjector)
```
The bus is a god-object in this package — used for fire-and-forget lifecycle events. Per-event channels (`IPublisher<BeforeViewOpenEvent>`, MessagePipe-style) would let consumers depend only on the topics they care about. Today every navigation type holds the whole bus.

**`com.scaffold.navigation/Runtime/Implementation/Before/AfterViewOpen/CloseEvent.cs:6`** — five trivial event records:
```csharp
public record AfterViewCloseEvent(Type ViewType) : ContextEvent;
```
Five files for five one-line records. The package's `ContextEvent` constraint forces this granularity. Fine for clarity, but a per-event-type dispatch is wasted because every consumer listens for "all four lifecycle hooks". A single `record ViewLifecycleEvent(ViewLifecyclePhase Phase, Type ViewType)` would collapse 4 raises and 4 subscriptions into 1 raise + 1 switch.

**`com.scaffold.navigation/Tests/NavigationInstallerAndInjectionTests.cs:153-178`** — the consumer wrote a hand-rolled `FakeEventBus` with six no-op methods because:
```csharp
private sealed class FakeEventBus : IEventBus { /* six empty methods */ }
```
This is the strongest "interface-too-wide" signal. If `IEventBus` had a `IPublisher`/`ISubscriber` split (audit's recommendation re: `IDynamicEventBus`), the test would only need to fake `IPublisher`. Today every test that needs an event bus pays the same six-method tax.

**`com.scaffold.navigation/Runtime/Utility/NavigationExtensions.cs:4-5, 8-9`** — `using Scaffold.Events.Contracts;` and `using Scaffold.Events;` are completely unused in this file (the file has zero events code). Same `autoReferenced` blast-radius pattern as `Scaffold.Types`. `NoView.cs` and `ViewSchema.cs` have the same dead imports.

**Zero usage** of: `IEventBus.Clear()`, `IEventBus.AddListener(Type, Action<ContextEvent>)` (the non-generic dynamic surface). Audit's recommendation to extract `IDynamicEventBus` would remove dead surface from every consumer's mock.

## Alternatives & prior art

- **MessagePipe (Cysharp)** — VContainer-friendly typed pub/sub; zero-alloc steady-state, async handlers, filter pipeline. `https://github.com/Cysharp/MessagePipe`. **Adopt (wrap)**: register MessagePipe in VContainer, keep `IEventBus` as a thin façade for migration, then deprecate. The package's whole feature set is a strict subset of MessagePipe's.
- **R3 (Cysharp)** — observable streams for .NET/Unity, replaces UniRx. `https://github.com/Cysharp/R3`. **Steal pattern**: for view-model binding (`IObservable<T>` per topic) where you want filtering/throttling. Don't replace bus dispatch.
- **Zenject `SignalBus`** — Zenject's typed signal bus. `https://github.com/modesttree/Zenject/blob/master/Documentation/Signals.md`. **Steal pattern**: the `DeclareSignal<T>` / typed `Fire<T>` API is exactly the typed `Raise<T>` the audit recommends. We're on VContainer, not Zenject — copy the API shape, not the lib.
- **VContainer's built-in `IObjectResolver` events** — none exist; VContainer is DI-only. Confirms a bus is justified. **Build (refactor path)**: keep `IEventBus` if MessagePipe is rejected, but implement the audit's typed-`Raise<T>` change. ~50 LOC delta vs ~5k LOC dependency.

## Benchmark plan

- **`Raise<T>` typed dispatch vs `Raise(ContextEvent)` reflection**
  - What: ns/op and allocations for a single raise with 0/1/N subscribers.
  - Tool: `Unity.PerformanceTesting`, `SampleGroup(AllocatedManagedMemory + Time)`.
  - Location: `Tests/Performance/EventBusBenchmarks.cs`.
  - Scenario: 1k raises × {0, 1, 10, 100} subscribers; 5 warmup, 10 iterations.
  - Baseline: today, `evt.GetType()` lookup ≈ 30 ns + dictionary probe per raise; record allocs ~40 B.
  - Success: typed `Raise<T>` ≤ 10 ns + 0 B at 0/1 subscriber; ≤ 2× MessagePipe baseline at 100 subscribers.

- **AddListener / RemoveListener closure allocation**
  - What: bytes allocated per `AddListener<T>(handler)` (the `e => handler((T)e)` wrapper at `Runtime/Implementation/EventController.cs:21`).
  - Tool: `Unity.PerformanceTesting`.
  - Location: `Tests/Performance/EventBusBenchmarks.cs`.
  - Scenario: 1000 add/remove cycles, single event type, 5 warmup.
  - Baseline: ~64 B per AddListener (closure + delegate + dict entry).
  - Success: 0 alloc on `RemoveListener` (dictionary lookup only); AddListener alloc ≤ 64 B (cannot eliminate closure unless API changes to `Action<ContextEvent>`-typed adapter).

- **Double-subscribe / unknown-unsubscribe behavior**
  - What: throughput when a buggy caller subscribes twice (current silent-pass path).
  - Tool: `Unity.PerformanceTesting` micro.
  - Location: `Tests/Performance/EventBusBenchmarks.cs`.
  - Scenario: 100 sub + 100 dup-sub; verify no growth in handler list.
  - Baseline: today, `+=` chains the delegate; raise calls handler twice.
  - Success: with audit's "throw on double-subscribe" change, this benchmark becomes a correctness test that throws — keep as a regression guard, not a perf test.

- **`Clear()` cost on a populated bus**
  - What: time + GC pressure clearing 1000 subscribers across 50 event types.
  - Tool: `Unity.PerformanceTesting`.
  - Location: `Tests/Performance/EventBusBenchmarks.cs`.
  - Scenario: 1× clear after seeding; measure single op.
  - Baseline: O(n) dictionary clear.
  - Success: ≤ 1 ms for the seeded scenario; verify zero heap retention after clear.
