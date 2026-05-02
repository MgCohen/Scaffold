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
