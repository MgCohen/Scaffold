# Audit: `com.scaffold.view`

Audited: 2026-05-02. Reviewer: senior architect (audit pass).
Path: `/home/user/Scaffold/Assets/Packages/com.scaffold.view`
Asmdef: `Scaffold.MVVM.View` (`noEngineReferences: false`).

## 1. Summary

The Unity-side of the MVVM stack: `View<T>`, `ViewElement<T>`, `UIView<T>`, `ViewComponent<T>`, plus a typed event bus (`ViewEvents` + `EventLedger<T>`). The base `ViewElement` derives from `MonoBehaviour` and hosts the typed binding entry. Pairing is **typed at compile time** (`View<T> where T : IViewModel`) and the cast in `ViewElement<T>.Bind` throws on mismatch, which is the right behavior. The event ledger is a hand-rolled per-type registry with two parallel storage shapes (typed + generic) and bubbles up the transform hierarchy. Subscriptions to `INotifyPropertyChanged` are wired but **never unsubscribed** — that is the most serious issue in this package.

**Verdict: keep with refactor.** The shape is correct and the typed pairing is the architect's pattern, but the lifecycle wiring (subscribe/unsubscribe, `Unbind` discoverability), the event-ledger boxing, and a half-dozen `Debug.Log` debug spew calls in production code paths need attention.

## 2. Structure

```
com.scaffold.view/
├── Runtime/
│   ├── AssemblyInfo.cs                     # InternalsVisibleTo Scaffold.MVVM.View.Tests
│   ├── BaseEvents/
│   │   ├── ClickViewEvent.cs               # ViewEvent + id string
│   │   └── NavigateViewEvent.cs            # uses Scaffold.Types.TypeReference
│   ├── Contracts/
│   │   ├── IEventLedger.cs                 # untyped Raise/Register/Unregister
│   │   ├── IView.cs                        # extends Scaffold.Navigation.Contracts.IView
│   │   └── IViewEvent.cs                   # Source/Current/IsConsumed/Consume/Restore/LogNext
│   ├── EventLedger.cs                      # generic + typed callback storage
│   ├── EventLedgerExceptionMode.cs         # Report / Throw
│   ├── EventLedgerExceptionOptions.cs
│   ├── Scaffold.MVVM.View.asmdef           # noEngineReferences: false (correct here)
│   ├── UiFloatBehaviour.cs                 # standalone idle-anim behavior, unrelated to MVVM
│   ├── UIView.cs                           # View<T> + Canvas/CanvasScaler scaffolding
│   ├── View.cs                             # View<T> : ViewElement<T> + IView lifecycle
│   ├── ViewComponent.cs                    # empty subclass of ViewElement<T>
│   ├── ViewContextRegistry.cs              # per-view IViewContext
│   ├── ViewElement.cs                      # MonoBehaviour base, partial, [BindSource]
│   ├── ViewElementT.cs                     # ViewElement<T : IViewModel>
│   ├── ViewElementT2.cs                    # ViewElement<TParent, TVm>
│   ├── ViewEvent.cs                        # base event class
│   ├── ViewEvents.cs                       # static facade + exception-options
│   └── ViewScaling.cs                      # enum
├── Samples/
│   ├── MVVMUseCases.cs                     # SampleViewModel + SampleView demo
│   └── Scaffold.MVVM.Samples.asmdef
├── Tests/
│   ├── Scaffold.MVVM.View.Tests.asmdef
│   └── ViewLifecycleAndContextTests.cs     # 3 tests (lifecycle, auto-bind, view-context)
├── README.md
└── package.json                            # depends on types, navigation, ugui, mvvm, viewmodel
```

Asmdef references: `Scaffold.MVVM.ViewModel`, `Scaffold.MVVM`, `Scaffold.Navigation`, `Scaffold.Types`. `overrideReferences: false` (so all engine assemblies are implicit). `autoReferenced: true`. Engine-aware: correct here, this is the Unity layer.

Tests cover: basic `Open/Hide/Focus/Close` flag-passing, `AutoBindChildViewComponents` once-per-bind verification, `ViewContext.Register/TryResolve`. Missing: `Unbind` clearing INPC subscriptions, repeated bind without leak, event-ledger bubbling, `Consume()`/`Restore()`, exception-mode behavior under throwing callbacks, type-mismatch throw path on `Bind(ViewModel)`.

## 3. What's good

- **Compile-time-typed View ↔ ViewModel pairing**. `View<T> where T : IViewModel` (`View.cs:8`), `ViewElement<T> where T : IViewModel` (`ViewElementT.cs:11`). The cast in `Bind(IViewController)` (`ViewElementT.cs:18-23`) is a typed `switch`-pattern and **throws on mismatch** with a useful message — fail-fast at the boundary, exactly as the architect prefers.
- **Sealed `Bind` on the typed element** (`ViewElementT.cs:16`) prevents subclasses from accidentally overriding the type-resolution and breaking the contract. Good.
- **`OnBind` / `OnUnbind` template methods** (`ViewElement.cs:37-49`) keep subclass hooks empty by default — no virtual ceremony, no required overrides.
- **`AutoBindChildViewComponents` is opt-in** (`View.cs:26`, defaults to `false`). Good defaults; reflection-walk only happens when explicitly requested.
- **Source-gen-driven binding API**. `[BindSource(typeof(TreeBinding))]` on `ViewElement` (`ViewElement.cs:11`) means consumers' `OnBind` writes `Bind(() => viewModel.Foo, x => label.text = x.ToString())` and the generator wires the rest. The view does not hand-roll `INotifyPropertyChanged` subscription/dispatch logic.
- **Typed event bus**: `EventLedger<T>` is generic (`EventLedger.cs:9`). `ViewEvents.Raise<TEvent>(MonoBehaviour, TEvent) where TEvent : ViewEvent` is the typed front door. Events bubble up the transform hierarchy with `Consume()` short-circuit (`EventLedger.cs:90-94`). Pattern is similar to UGUI's `EventSystems.ExecuteEvents` but typed.
- **Exception-handling policy is configurable** via `EventLedgerExceptionMode` (`ReportAndContinue` / `ThrowAfterDispatch`) and `EventLedgerExceptionOptions` with a custom reporter delegate. `PushExceptionOptions(...)` returns an `IDisposable` scope (`ViewEvents.cs:29-36, 222-247`) — correct C# idiom.
- **`IViewContext` is a small per-view DI scope** (`ViewContextRegistry.cs`), kept distinct from VContainer so views can register scene-local services without a container rebuild. Lazy-initialized (`View.cs:22-24`). Sound design.
- **`UIView<T>` requires a Canvas/CanvasScaler** via `[RequireComponent]` (`UIView.cs:10-11`) — Unity-correct way to declare the dependency.
- **`UiFloatBehaviour` resets `anchoredPosition` in `OnDisable`** (`UiFloatBehaviour.cs:45-51`) — preserves layout when toggled. Small but correct detail most authors miss.

## 4. Issues / smells

### 4.1 INPC subscription is never removed — concrete memory-leak risk

`ViewElementT.cs:34-45`:

```csharp
private void WireViewModelSubscriptions(T vm)
{
    if (vm is INotifyPropertyChanged npc)
    {
        npc.PropertyChanged -= OnViewModelChanged;
        npc.PropertyChanged += OnViewModelChanged;
    }
    if (vm is INestedObservableProperties nop)
    {
        nop.RegisterNestedProperties();
    }
}
```

Subscribed in `Bind`. **`Unbind` never unsubscribes**. `ViewElement.Unbind` (`ViewElement.cs:41-45`) calls `ClearBindings()` (the source-gen'd binding teardown) and `OnUnbind()`, but **does not** call `npc.PropertyChanged -= OnViewModelChanged`. So the View's `OnViewModelChanged` handler stays subscribed to the old viewmodel's `PropertyChanged` event after the view rebinds to a new one. Two consequences:

1. The old VM holds a strong reference to the old view (via the delegate target). If the view is destroyed before the VM is, you get the classic Unity "MonoBehaviour destroyed but not null" UI behavior — calls to `gameObject.name`/`UpdateBinding` on a destroyed component will silently log "ScriptingBehaviour destroyed" warnings. Not a memory leak in the usual GC sense (the VM holds the *destroyed* view), but a defect leak.
2. If `Bind(newVm)` is called, the **new** VM gets a fresh subscription, and the **old** VM still has the same `OnViewModelChanged` instance subscribed because line 38 does `npc.PropertyChanged -= OnViewModelChanged;` on the *new* VM (parameter `vm`), not the prior one. The old subscription on the old VM is leaked.

**Fix**: track the currently-subscribed `INotifyPropertyChanged` reference in a private field, unsubscribe on `Unbind` and on rebind before re-subscribing.

### 4.2 `Bind` does not reset state if called with `null`

`ViewElement.cs:29-35`:

```csharp
public virtual void Bind(IViewController viewModel)
{
    if (viewModel == null)
    {
        return;
    }
}
```

Silent no-op on null in the base. `ViewElementT.Bind` (`ViewElementT.cs:16-32`) handles `null` by setting `viewModel = default` then **only calls `Unbind` if the previous `viewModel` was non-default** (line 24-27), but it then sets `this.viewModel = vm` (which is `default`), logs, and *still* calls `WireViewModelSubscriptions(default)` — which performs `default is INotifyPropertyChanged npc` → false (for class T), so it's a no-op. Net effect: Bind(null) on a previously-bound view runs `Unbind` (good) but also calls `OnBind` (bad, the view believes it's now bound). The test (`ViewLifecycleAndContextTests.cs`) does not cover this. Should `Bind(null)` mean "Unbind"? Pick one and document.

### 4.3 `EqualityComparer<T>.Default.Equals(viewModel, default)` is a boxing trap for value types

`ViewElementT.cs:24`:

```csharp
if (!EqualityComparer<T>.Default.Equals(viewModel, default))
{
    Unbind();
}
```

`T : IViewModel` constrains to interface, so `T` could in theory be a struct viewmodel. In practice it'll be a class. `EqualityComparer<T>.Default` avoids boxing for primitives but allocates a comparer instance the first time. Negligible; the more important issue is **semantic**: this checks "is it not the default value," and for a class the default is null. `viewModel != null` is clearer. Same outcome, less allocation, less misdirection. Use:

```csharp
if (viewModel is not null) Unbind();
```

(Yes, `viewModel != null` on a `MonoBehaviour`-typed reference would do the lifetime-aware Unity check; but `T : IViewModel` is *not* a MonoBehaviour, so `is not null` is safe and right.)

### 4.4 `Debug.Log` spam in hot paths

- `ViewElement.cs:21`: `Debug.Log("View element update : " + name + " - " + propertyName)` on **every** property-change notification. In a busy UI this is hundreds of logs per second.
- `ViewElement.cs:23` builds a `string.Join('.', ...)` string for the same path on every notification regardless of logging.
- `ViewElementT.cs:29`: `Debug.Log("Registering view model " + GetType().Name + " - " + typeof(T).Name)` on every `Bind`.
- `BindedProperty.cs` and `BindedCollection.cs` (in `com.scaffold.mvvm`) also log; covered in that package's audit.

These are debug instrumentation that should be `[Conditional("SCAFFOLD_MVVM_VERBOSE")]` or routed through an `IBindingDiagnostics` injected facade. Production builds will pay for the string concatenations even if `Debug.Log` is later disabled, because string allocation precedes the call.

### 4.5 `EventLedger<T>` boxes `IViewEvent` in the generic-callback path

`EventLedger.cs:11-12`:

```csharp
private readonly Dictionary<Transform, List<Action<T>>> callbackList = new();
private readonly Dictionary<Transform, List<Action<IViewEvent>>> genericCallbackList = new();
```

The typed-callback dictionary (`Action<T>`) is fine. The `genericCallbackList` always boxes the event arg into an `IViewEvent` parameter — `T` is constrained to `ViewEvent` (a class), so technically no boxing on cast, just an interface dispatch. But every callback registered via the non-generic `Register(Type, MonoBehaviour, Action<IViewEvent>)` (`ViewEvents.cs:95-101`) goes through the generic list. If your codebase only ever uses the typed `Register<TEvent>`, the generic list is dead weight; if it doesn't, you pay double bookkeeping per event type. Pick one path: either drop the generic registration API or back both with a single typed list and synthesize the typed callback for generic registrants.

### 4.6 `EventLedger<T>` does not clean up dead transforms

The dictionaries are keyed by `Transform`. When a `GameObject` is destroyed the `Transform` becomes a "fake null" Unity reference — but the dictionary still holds the C# reference (Unity's `==` overload returns true vs C# `==` returns false). Over time this dictionary grows monotonically. There's no `Cleanup()` API. Either expose explicit unregister paths consumers must call (already in place for callbacks but not for *transforms with empty lists*), or run a sweep in the static `ViewEvents` periodically. Today a scene reload that destroys 1000 listener GOs leaves 1000 stale transform keys in the static `ledgers` dictionary.

Compare to **MessagePipe** (https://github.com/Cysharp/MessagePipe) which integrates with VContainer scopes and disposes subscriptions when the scope dies — that's the Unity-MVVM idiom worth adopting.

### 4.7 `ViewEvents` is a static singleton

`ViewEvents.cs:11-12`:

```csharp
private static readonly Dictionary<Type, IEventLedger> ledgers = new();
private static EventLedgerExceptionOptions exceptionOptions = CreateDefaultExceptionOptions();
```

Static state crosses domain reloads only inside the editor. In Player, scene-to-scene state survives indefinitely. There's no `ResetStatics` for domain-reload-on-play-mode-disabled (Unity 2019.3+ feature). Add `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` to clear `ledgers` on play start, and tag the type with the Unity `[Configurable]`-style attributes if the project uses them.

### 4.8 Two parallel registration APIs (typed + Type-based) duplicate guard logic

`ViewEvents.cs:80-93` (typed) and `ViewEvents.cs:95-101` (Type-based) have nearly identical bodies. The Type-based path uses `GuardRegisterInput` (lines 166-171) — a 3-line wrapper of three other 1-line wrappers (`GuardEventType`, `GuardEventListener`, `GuardCallback`). This is the "redundant guard clauses" pattern the architect dislikes — six methods to throw three `ArgumentNullException`s. Compress to one private guard or just inline the three `if` checks.

### 4.9 `EventLedger<T>.RaiseListCallbacks` iterates **backwards** but ignores `IsConsumed` after first dispatch

`EventLedger.cs:110-117`:

```csharp
private void RaiseListCallbacks<TEvent>(List<Action<TEvent>> callbacks, TEvent evt, ref List<Exception> callbackExceptions)
{
    for (var i = callbacks.Count - 1; i >= 0; i--)
    {
        if (evt.IsConsumed) return;
        InvokeCallback(callbacks[i], evt, ref callbackExceptions);
    }
}
```

Backwards iteration is a workaround for callbacks that unregister themselves — fine. But the bubbling logic in `RaiseAtTransform` runs both `typedCallbacks` and `genericCallbacks` in sequence (`EventLedger.cs:99-108`); if a typed callback consumes the event, the **generic** list's first callback is *still invoked* because `RaiseListCallbacks` only checks `IsConsumed` at the *top* of its loop, and `RaiseAtTransform` does not check `evt.IsConsumed` between the two list invocations. Off-by-one: drop the second `RaiseListCallbacks(genericCallbacks, ...)` if `evt.IsConsumed`. Cheap fix.

### 4.10 `evt.LogNext(transform)` mutates the event mid-bubble even if no handlers

`EventLedger.cs:99-101`: `evt.LogNext(transform)` is called for **every** transform on the ancestry walk regardless of whether the transform has any registered callbacks. If you bubble through 30 transforms and only the root listens, you've appended 30 entries to `evt.History`. The `History` list is `public List<Transform>` (`ViewEvent.cs:35-36`) — exposed mutably. Either guard `LogNext` with "did this transform have callbacks" or stop logging during bubble.

### 4.11 `ViewEvent.PointerData` is `[SerializeField]` — Unity will try to serialize it

`ViewEvent.cs:29`: `[SerializeField] private PointerEventData pointerData;`. `PointerEventData` is a UGUI runtime type; serializing it to a scene/prefab makes no sense. Same for `Transform Source/Current/Consumer` (lines 32, 34, 39), `List<Transform> History` (line 36), and `Transform consumer` (line 39). These fields are all set during runtime dispatch only. Remove `[SerializeField]` or add `[NonSerialized]`. The class is `[Serializable]` (line 12) which compounds the issue if the event is embedded in a MonoBehaviour field (it isn't visibly, but `NavigateViewEvent` *is* `[Serializable]` for inspector use, line 11 of `NavigateViewEvent.cs`).

Compare: `NavigateViewEvent` correctly serializes `navigation`, `view`, `closeCurrent` (configuration-time data) and inherits the runtime fields. Inspector will show all the runtime garbage too unless they're hidden.

### 4.12 `View.cs:115-136` reflection walk on every Bind for `AutoBindChildViewComponents`

`View.TryParseViewElementModelType` walks the `BaseType` chain to find `ViewElement<>`. This runs **for every child `ViewElement` in `GetComponentsInChildren<ViewElement>(true)`** on every bind when `AutoBindChildViewComponents` is true. That's reflection per child per bind. Cache the type→ViewModelType result in a static `Dictionary<Type, Type>`. One-time cost.

### 4.13 `ViewElementT2.cs` has `Bind(T parent, J viewModel)` that silently no-ops on null `viewModel`

`ViewElementT2.cs:11-23`:

```csharp
public void Bind(T parent, J viewModel)
{
    if (parent == null) throw new ArgumentNullException(nameof(parent));
    if (viewModel == null) return;
    this.parent = parent;
    Bind(viewModel);
}
```

Asymmetric: `parent` null throws, `viewModel` null silently returns. Pick one fail-fast policy. Architect's preference says: throw.

### 4.14 `ViewComponent<T>` is an empty class

`ViewComponent.cs:9-11`:

```csharp
public class ViewComponent<T> : ViewElement<T> where T : IViewModel
{
}
```

Pure type-tagging. If the only point is to distinguish "component within a view" from "view element root," an attribute or marker interface would be cheaper. If there's a planned divergence from `ViewElement<T>`, leave a `// TODO` comment so future maintainers know why it exists.

### 4.15 `UIView<T>.Order` and `OnOpen` repeatedly call `SetCanvas`

`UIView.cs:17-22, 24-28`. `SetCanvas` does `GetComponent<Canvas>()` if cached null, `Camera.main` lookup (slow), then `SetCanvasScale`. `Camera.main` is an FindObjectOfType under the hood and is cached only if the cached one is alive. Calling on `Order` (set every time view ordering changes) and `OnOpen` (every show) means a lot of `Camera.main` lookups. Cache once per scene.

### 4.16 `View.cs:30-64` Hide-vs-Close-vs-Open lifecycle hooks via boolean flags

`OnOpen(bool wasHidden)`, `OnClose(bool hiding)` — using `bool` to discriminate semantics means consumers write `if (wasHidden) ... else ...` — exactly the kind of split flow that suggests two separate methods (`OnReveal` vs `OnFirstOpen`, `OnHide` vs `OnDestroy`). Mild smell; the README acknowledges this is intentional but consumers will write the same `if` ladder repeatedly.

### 4.17 `ViewEvents.SetExceptionOptions(null)` throws — but the field is mutable

The static field `exceptionOptions` (`ViewEvents.cs:12`) is private. Setter throws on null. `EnsureExceptionOptionsState()` (lines 214-220) defensively re-checks and throws if it's somehow null. Three layers of guarding for one private field that the only setter already validates. Pick one: trust the setter and drop the runtime check, or expose a constant default. Architect explicitly dislikes layered guards.

### 4.18 `EventLedger.Raise(Transform, IViewEvent)` casts and rethrows generic exception

`EventLedger.cs:64-69`:

```csharp
void IEventLedger.Raise(Transform transform, IViewEvent evt)
{
    if (evt is not T typedEvent)
    {
        throw new Exception($"Trying to raise event of wrong type, tried to raise {evt.GetType()} instead of {typeof(T)}");
    }
    Raise(transform, typedEvent);
}
```

`throw new Exception(...)` should be `throw new InvalidCastException(...)` or a specific typed exception. `Exception` defeats catch-blocks that try to be specific. `ViewElementT.cs:22` has the same pattern. Replace with a typed exception (or a defined `MvvmTypeMismatchException`).

### 4.19 `UiFloatBehaviour` is dead-weight in this package

`UiFloatBehaviour.cs` is a small UI animation utility unrelated to MVVM. It belongs in a `com.scaffold.ui-utilities` or similar, not in the MVVM view package. Adds noise to the package.

## 5. Suggested before/after

### Unsubscribe INPC on Unbind (most important)

**Before** (`ViewElementT.cs:34-45` and `ViewElement.cs:41-45`):

```csharp
private void WireViewModelSubscriptions(T vm)
{
    if (vm is INotifyPropertyChanged npc)
    {
        npc.PropertyChanged -= OnViewModelChanged;
        npc.PropertyChanged += OnViewModelChanged;
    }
    ...
}

protected void Unbind()
{
    ClearBindings();
    OnUnbind();
}
```

**After:**

```csharp
private INotifyPropertyChanged subscribed;

private void WireViewModelSubscriptions(T vm)
{
    UnwireViewModelSubscriptions();
    if (vm is INotifyPropertyChanged npc)
    {
        npc.PropertyChanged += OnViewModelChanged;
        subscribed = npc;
    }
    if (vm is INestedObservableProperties nop) nop.RegisterNestedProperties();
}

private void UnwireViewModelSubscriptions()
{
    if (subscribed is null) return;
    subscribed.PropertyChanged -= OnViewModelChanged;
    subscribed = null;
}

protected void Unbind()
{
    UnwireViewModelSubscriptions();
    ClearBindings();
    OnUnbind();
}
```

This eliminates the leak in 4.1 and gives a single audited subscribe/unsubscribe pair.

### Bubbling check between typed and generic callback lists

**Before** (`EventLedger.cs:99-108`):

```csharp
private void RaiseAtTransform(Transform transform, T evt, ref List<Exception> callbackExceptions)
{
    evt.LogNext(transform);

    callbackList.TryGetValue(transform, out var typedCallbacks);
    genericCallbackList.TryGetValue(transform, out var genericCallbacks);

    if (typedCallbacks != null) RaiseListCallbacks(typedCallbacks, evt, ref callbackExceptions);
    if (genericCallbacks != null) RaiseListCallbacks(genericCallbacks, evt, ref callbackExceptions);
}
```

**After:**

```csharp
private void RaiseAtTransform(Transform transform, T evt, ref List<Exception> callbackExceptions)
{
    callbackList.TryGetValue(transform, out var typedCallbacks);
    genericCallbackList.TryGetValue(transform, out var genericCallbacks);

    bool hasAny = (typedCallbacks?.Count > 0) || (genericCallbacks?.Count > 0);
    if (!hasAny) return;

    evt.LogNext(transform);
    if (typedCallbacks is { Count: > 0 }) RaiseListCallbacks(typedCallbacks, evt, ref callbackExceptions);
    if (evt.IsConsumed) return;
    if (genericCallbacks is { Count: > 0 }) RaiseListCallbacks(genericCallbacks, evt, ref callbackExceptions);
}
```

Fixes 4.9 (consume between lists) and 4.10 (skip `LogNext` when nothing listens).

### Throw typed exception on view/VM mismatch

**Before** (`ViewElementT.cs:18-23`):

```csharp
T vm = viewController switch
{
    T typed => typed,
    null => default,
    _ => throw new Exception($"Trying to bind view {GetType()} to controller of type {viewController.GetType()}, expected: {typeof(T)}"),
};
```

**After:**

```csharp
T vm = viewController switch
{
    T typed => typed,
    null => default,
    _ => throw new InvalidCastException(
        $"View {GetType()} cannot bind controller of type {viewController.GetType()}; expected {typeof(T)}."),
};
```

### Wrap diagnostic logs

```csharp
[System.Diagnostics.Conditional("SCAFFOLD_MVVM_VERBOSE")]
private static void TraceBind(string message) => UnityEngine.Debug.Log(message);
```

Then replace the four `Debug.Log` sites in `ViewElement.cs:21` and `ViewElementT.cs:29` with `TraceBind(...)` (deferred string formatting with `string.Concat` is still eager — pass a `Func<string>` or use a verbose-only path). Removes all production logging cost.

## 6. Easy wins (each <30 min)

1. Replace `EqualityComparer<T>.Default.Equals(viewModel, default)` with `viewModel is not null` (`ViewElementT.cs:24`).
2. Strip `[SerializeField]` from runtime-only fields in `ViewEvent.cs:29, 32, 34, 36, 39, 41, 44`.
3. Replace `throw new Exception(...)` with `InvalidCastException` in `EventLedger.cs:68` and `ViewElementT.cs:22`.
4. Add `viewModel == null` early-throw in `ViewElementT2.Bind(T, J)` (`ViewElementT2.cs:18-20`) — symmetric with `parent` check.
5. Wrap the `Debug.Log` calls in `ViewElement.cs:21` and `ViewElementT.cs:29` with `[Conditional("SCAFFOLD_MVVM_VERBOSE")]` static helpers.
6. Cache `Camera.main` in `UIView` to avoid the `FindObjectOfType` each `OnOpen`/`Order`.
7. Move `UiFloatBehaviour` to a UI-utilities package or document why it lives here.
8. Add a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` clear on `ViewEvents.ledgers` to handle "domain reload disabled" play-mode entry.

## 7. Bigger refactors

### R1. Replace `ViewEvents` static singleton with VContainer-scoped event bus (1-2 days)

The architect uses VContainer. The current `ViewEvents` static is invisible to DI, untestable without static reset, and accumulates dead transforms. Replace with `IViewEventBus` registered per-LifetimeScope, injected into views. Compare to **MessagePipe + VContainer** (https://github.com/Cysharp/MessagePipe#vcontainer-extension) which already does this with `IPublisher<T>` / `ISubscriber<T>`, scope-aware disposal, and zero-allocation dispatch.

This also resolves 4.6 and 4.7 in one swing.

### R2. Replace expression-tree binding with R3.Observable on the View side (research spike)

Each `[ObservableProperty]` could expose `Observable<T>` (R3) on the source class (generator emits this from the same `[ObservableProperty]` field). Views call `viewModel.Foo.Subscribe(value => label.text = value.ToString()).AddTo(this)`. No path strings, no `INotifyPropertyChanged += handler`, no leak risk because R3's `AddTo(MonoBehaviour)` ties the subscription to `OnDestroy`. Reference: https://github.com/Cysharp/R3#unityusage. This is a different model — talk through with team before committing.

### R3. Codify a "command" path on the View

Currently any user input (button click) raises a `ClickViewEvent` and bubbles up the transform tree until something listens. There's no direct "View → ViewModel command" channel. Three options:

- Adopt `[RelayCommand]` from CommunityToolkit (already shipped as a precompiled DLL): the view binds a `Button.onClick → vm.SubmitCommand.Execute`.
- Continue with `ClickViewEvent` and document it as the canonical command path.
- Both: events for cross-cutting (Navigate, Cancel) and `IRelayCommand` for view-local user actions.

The current state — DLL imported, no usage — leaves it unclear.

### R4. `View<T>.OnOpen(bool)` / `OnClose(bool)` → split into named methods

`OnFirstOpen()` / `OnReveal()` / `OnHide()` / `OnDestroyed()`. Removes the `if (wasHidden)` ladder consumers will keep writing.

## 8. Organization & docs

- README is consistent with the project standard. Examples are concrete and usable.
- The README claims **typed view/viewmodel pairing must remain enforced** as an invariant, and the code does so via the `T` constraint on `ViewElement<T>`. Good — codify this with an analyzer too: a Roslyn rule that bans `View<IViewModel>` (the unbound generic) and forces a concrete VM type.
- `MVVMUseCases.cs` (in `Samples/`) demonstrates the end-to-end binding flow but the sample's `BuildSampleModel : Model` references a `Model` base class from `com.scaffold.model` which is not declared as a dependency in this package's `package.json`. That sample will not compile without that other package. Either declare the dep on the samples asmdef, or rewrite the sample to be View-only.
- The `BaseEvents/` folder has only two files (`ClickViewEvent`, `NavigateViewEvent`). If "base events" is the intended idiom for a small registry of well-known events, document the naming convention. Otherwise consolidate into a single file or move both into the package root.
- No XML docs on any public type. The publicly-consumed surface — `View<T>`, `ViewElement<T>`, `ViewEvents`, `EventLedgerExceptionMode`, `EventLedgerExceptionOptions`, `IViewContext` — is all undocumented. Triple-slash these. The README is good but XML docs are what the IDE shows on hover.
- `IView` (`Runtime/Contracts/IView.cs`) is a 1-line interface that just inherits from `Scaffold.Navigation.Contracts.IView`. Either extends with view-specific contract or is dead inheritance. Today it's the latter.

## References

- CommunityToolkit MVVM `[RelayCommand]`: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/relaycommand
- ReactiveUI `WhenActivated` and view lifetime tokens: https://www.reactiveui.net/docs/handbook/when-activated/
- Unity UI Toolkit data binding (string-pathed, illustrative counter-example): https://docs.unity3d.com/Manual/UIE-data-binding.html
- Cysharp MessagePipe + VContainer pub/sub: https://github.com/Cysharp/MessagePipe
- Cysharp R3 (Rx for .NET / Unity): https://github.com/Cysharp/R3
- Avalonia `x:CompileBindings` (compile-time-typed bindings): https://docs.avaloniaui.net/docs/data-binding/compiled-bindings
- Unity 2019.3+ "Domain Reload on Play Mode" notes (relevant to static `ledgers`): https://docs.unity3d.com/Manual/DomainReloading.html
