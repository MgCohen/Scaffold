# Audit — `com.scaffold.navigation`

Senior architect review. Tone: opinionated.

## 1. Summary & verdict

`com.scaffold.navigation` is the largest of the three packages (55 files) and the most ambitious. It is a stack-based UI navigation system with: a `NavigationStack` ordered list of `NavigationPoint`s, four stack policies (`Push`, `ReplaceCurrent`, `ClearBelowCurrentAndPush`, `ClearAllAndPush`), three view-source strategies (`Context`, `DirectPrefab`, `Addressables`), schema-driven transition + animation handlers, an event bus integration (`BeforeViewOpenEvent`, `AfterViewOpenEvent`, etc.), middleware (`INavigationOpenHandler`), a deferred `IViewControllerDependencyInjector` (default impl injects from the AppFlow `ILayerResolver.Top`), an Addressables handle pool + view instance pool, and a per-`ViewConfig` schema asset object.

Then it has, parked next to all that, a `ServerNavigationController` whose every method is a `GuardServerState` no-op (`if (this == null) throw`), a `NoView` whose every method is `throw new NotImplementedException`, a `NavigationOptions.CloseAllViews bool?` legacy parameter that the resolver still honors, an `INavigationMiddleware` marker interface with no methods, two `closeCurrent` parameters in `INavigation.Open` overloads that compete with `NavigationStackPolicy`, an `IViewContext` / `IViewContextHost` pair never referenced anywhere in the runtime, and a `NavigationController` constructor that null-checks every parameter individually with five-line `if`-blocks.

The architectural intent is correct — abstraction here is justified, this *is* a place that will keep changing. But the package has accumulated three or four overlapping ways to do the same thing and is half-migrated through several refactors at once. Verdict reflects that.

Critical issues:
- Stringly-typed by `Type` and by config asset name; no compile-time route safety. `INavigation.Open<TController>(controller)` is nominally typed, but the controller→view binding is resolved at runtime by `Type.IsAssignableFrom` lookup against a serialized list. There is no source generator producing `Routes.MainMenu` keys.
- `Popup` vs `Screen` distinction exists (`ViewType` enum) but the stack treats them identically. Real popup semantics (modal stacking on top of a screen, dismiss without affecting screen stack) are not modeled.
- No back-press handling. No deep-link intake. No lifecycle hooks beyond raw `Bind`/`Open`/`Close`/`Hide`/`Focus`/`Order`.
- `async void RunTransitions()` is a fire-and-forget unobserved task — exceptions are silently swallowed.
- Several public types are pure dead code (`ServerNavigationController`, `NoView`, `IViewContext`).
- Legacy / new APIs coexist without deprecation attributes.

**Verdict: refactor.** The bones of the design are reasonable; the surface needs aggressive deletion and one strong typing pass. Probably 30% of the runtime files are deletable today.

---

## 2. Structure

```
com.scaffold.navigation/
  Container/                                 (Scaffold.Navigation.Container.asmdef → Scaffold.AppFlow + VContainer)
    NavigationInjection.cs                   (INavigationOpenHandler + IViewControllerDependencyInjector)
    NavigationInstaller.cs                   (IInstaller, optional NavigationSettings ctor param)
  Editor/
    ViewConfigEditor.cs                      (CustomEditor, asset-vs-prefab UI)
  Runtime/                                   (Scaffold.Navigation.asmdef autoReferenced=true)
    AssemblyInfo.cs
    Contracts/                               (boundary types — INavigation, IView, IViewController,
                                              ViewType, ViewState, ViewAssetSource, ViewFilter,
                                              NavigationOptions, NavigationStackPolicy, AnimationType,
                                              AnimationHandler, TransitionHandler, TransitionDirection,
                                              INavigationMiddleware, INavigationOpenHandler,
                                              IViewControllerDependencyInjector, IViewContext, IViewContextHost,
                                              IViewTransitionHandler, IViewAnimationHandler)
    Implementation/                          (NavigationController, NavigationProvider, NavigationStack,
                                              NavigationStackResolver, NavigationTransitions,
                                              NavigationMiddlewares, NavigationPoint, NavigationPointStrategy
                                              [Context/Buffer/Addressables/DirectPrefab],
                                              NavigationAssetHandleBuffer, NavigationViewInstanceBuffer,
                                              NavigationSettings, NavigationOptionsSchema,
                                              ViewSchema, AnimationViewSchema, TransitionViewSchema,
                                              ViewConfig, ViewTransitionData, ViewChangedEvent,
                                              BeforeViewOpenEvent / AfterViewOpenEvent /
                                              BeforeViewCloseEvent / AfterViewCloseEvent,
                                              ServerNavigationController)
    Providers/NavigationAssetProvider.cs     (Addressables-loaded NavigationSettings)
    Utility/NavigationExtensions.cs          (OpenReplace / OpenClearBelowAndPush / OpenClearAllAndPush)
    Utility/NoView.cs                        (throws on every member; only used as a sentinel type)
  Samples/NavigationUseCases.cs              (a class with two methods + two private inner stubs)
  Tests/                                     (NavigationStackResolverTests + NavigationInstallerAndInjectionTests)
```

Asmdef cross-references look correct. `Scaffold.Navigation.asmdef` references `Scaffold.Events`, `Scaffold.Addressables`, `Unity.Addressables`, plus three GUIDs (likely `Scaffold.Types`, `Scaffold.Schemas`, `Scaffold.Records` — the package depends on them per `package.json`). `Scaffold.Navigation.Container` cleanly isolates VContainer wiring — the architect should propagate this split to SceneFlow (see SceneFlow audit §7.2).

---

## 3. What's good

- **Container split.** `Scaffold.Navigation.Container` separates DI wiring from the runtime so the runtime is portable. Keep this pattern.
- **`NavigationStackPolicy` enum** is a legit improvement over the old `closeCurrent` bool; the four named policies (Push / ReplaceCurrent / ClearBelowCurrentAndPush / ClearAllAndPush) cover the realistic UI cases.
- **Strategy pattern for view sources.** `INavigationPointStrategy` with `ContextNavigationPointStrategy`, `BufferNavigationPointStrategy`, `DirectPrefabNavigationPointStrategy`, `AddressablesNavigationPointStrategy` (`NavigationProvider.cs:18-23`) is a clean way to layer cache → addressable load. The order matters and is correct (context → buffered instance → addressable load).
- **Asset handle pool + instance buffer.** `NavigationAssetHandleBuffer` + `NavigationViewInstanceBuffer` keep prefab handles resident for navigation lifetime. Sensible memory model.
- **Typed events.** `BeforeViewOpenEvent`, `AfterViewOpenEvent`, etc. as `record`s with `Type ViewType` — good for cross-cutting analytics.
- **Tests on stack resolver.** `NavigationStackResolverTests.cs` — small but sharp tests of the policy resolution truth table.
- **Container tests.** `NavigationInstallerAndInjectionTests.cs` exercises the installer with optional settings and confirms `INavigationOpenHandler` and `IViewControllerDependencyInjector` are the same instance (so middleware vs. injector is one object, no double-registration).
- **`NavigationPoint.AwaitReadyAsync`** correctly defers transitions on async-loaded views (`NavigationPoint.cs:112-114`, used in `NavigationTransitions.cs:53`).
- **`ViewConfig.OnValidate`** auto-resolves controller type from the prefab's view-side generic base (`ViewConfig.cs:30-114`) — nice editor UX.

---

## 4. Issues & smells

### 4.1 Routes are not typed at compile time

`INavigation.Open<TController>(TController controller, ...)` (`INavigation.cs:5-7`) is generic on the controller, but the *destination view* is resolved at runtime via `NavigationSettings.GetViewConfig(typeof(TController))`, which does `screens.FirstOrDefault(s => s.ControllerType.IsAssignableFrom(type))` (`NavigationSettings.cs:36`). If a controller has no `ViewConfig`, you get `throw new Exception($"No view config found for {type.Name}")` — *runtime* failure.

Per the rubric ("Prefer generics and C# typing for compile-time safety"), this is the central refactor opportunity. Two patterns to consider:

1. **Typed route generated from settings.** Source generator scans the `NavigationSettings` asset (or `[ViewRoute]`-attributed controllers) and emits `Routes.MainMenu`, `Routes.Settings` properties typed by their controller. `INavigation.Open(Routes.MainMenu)` becomes the canonical call.
2. **Marker-typed `ViewConfig`.** `ViewConfig<TController, TView>` lets the asset itself encode the binding; `INavigation.Open<TController>(TController)` resolves through a typed dictionary keyed by `typeof(TController)`, with a Roslyn analyzer warning when a controller is opened that has no asset (compile-time list of `ViewConfig` assets known via attribute or generated registry).

Either way, get rid of `throw new Exception($"No view config found for {type.Name}");` (`NavigationSettings.cs:39`) — a stringly-typed error in a path that could be compile-time-checked.

### 4.2 `async void RunTransitions()` — unobserved exceptions

`NavigationTransitions.cs:38-46`:

```csharp
private async void RunTransitions()
{
    runningTransition = true;
    while (pendingTransitions.Count > 0)
    {
        await ProcessNextTransition();
    }
    runningTransition = false;
}
```

`async void` is correct for top-level event handlers in WinForms. It is **never** correct for an internal queue drainer. If `ProcessNextTransition` throws (e.g. `NavigationPoint.FailReady` because Addressables load failed), the exception escapes to `SynchronizationContext.UnhandledExceptionEventHandler` (Unity logs it as an uncaught error), `runningTransition` stays `true`, and the queue stalls forever. Subsequent `DoTransition` enqueues will sit there silently.

Fix: make it `async Task`, store the task in a `Task currentTransitionLoop`, and observe it. Wrap the body in `try/catch` that reports through `IAppFlowErrorHandler` (or a navigation-specific error sink). At minimum, reset `runningTransition = false` in `finally`.

### 4.3 Popups vs screens are not actually distinct

`ViewType.cs`:

```csharp
public enum ViewType { Screen = 0, Popup = 1 }
```

`NavigationTransitions.cs:113`:

```csharp
bool shouldHide = transition.To.View == null || transition.To.View.gameObject == null
                  || transition.To.View.Type is ViewType.Screen;
if (shouldHide) transition.From.View.Hide();
```

That is the *only* place `ViewType.Popup` makes a behavioral difference: when the target is a Popup, the previous view isn't hidden. That is the entire popup model. There is:

- No popup-stack separate from screen-stack.
- No "dismiss popup, keep screen alive."
- No modal blocking of the underlying screen's input (canvas raycaster is still on).
- No popup-specific lifecycle hook (e.g. `OnDismissedExternally`).
- No back-press = "close topmost popup if any, otherwise pop screen."

If popups are a real product concept (and per the rubric they should be modeled separately), promote them. Otherwise delete `ViewType.Popup` — half-modeling is worse than not modeling.

### 4.4 No back-press, no deep-link, no lifecycle hooks

The cross-cutting questions:

- **Back-press**: Unity's `Input.GetKeyDown(KeyCode.Escape)` or new Input System "Cancel" action — no integration. There is no `INavigation.HandleBack()` that pops the stack with awareness of popups. Every consumer has to wire this themselves and call `Return()` — and `Return()` does not know about popups.
- **Deep-links**: no URI/route parsing, no `Open(Uri)` overload, no resumption-from-url. Common requirement for production apps.
- **Lifecycle hooks**: `IView` has `Open / Close / Focus / Hide / Order` (`IView.cs`), `IViewController` has `Bind(INavigation) / Close()` (`IViewController.cs`). There is no `OnAppearing/OnDisappearing`, no `OnSuspended/OnResumed` (app backgrounding), no `OnNavigatedFrom/OnNavigatedTo` with options/parameters. The events `Before/AfterViewOpenEvent` are coarse and broadcast, not per-view callbacks.

This is exactly the place where the rubric's "abstraction at places that will keep changing" applies. A typed lifecycle interface (`IViewLifecycle`) and a back-press router (`INavigationBackHandler`) belong here.

Reference patterns: MAUI Shell's `OnNavigatedTo` / `OnNavigatedFrom`, Android's `OnBackPressedDispatcher`, iOS's `viewWillAppear/viewDidDisappear`. The rubric mentions MAUI Shell explicitly.

### 4.5 `NavigationController` ctor — five sequential null checks

`NavigationController.cs:15-45`:

```csharp
public NavigationController(IEventBus events, NavigationSettings settings, Transform viewHolder,
    IEnumerable<INavigationMiddleware> middlewares, IAddressablesGateway addressablesGateway,
    IViewControllerDependencyInjector dependencyInjector)
{
    if (events is null) throw new System.ArgumentNullException(nameof(events));
    if (settings is null) throw new System.ArgumentNullException(nameof(settings));
    if (viewHolder is null) throw new System.ArgumentNullException(nameof(viewHolder));
    if (middlewares is null) throw new System.ArgumentNullException(nameof(middlewares));
    if (addressablesGateway is null) throw new System.ArgumentNullException(nameof(addressablesGateway));
    // ...
}
```

This is the rubric's "redundant guard clauses" pattern at the entry point. The architect's preference is *minimum* code. Use `ArgumentNullException.ThrowIfNull(events);` (one line per check, .NET 6+). Or even better, drop them — a DI container that resolves a null instance is already broken; at the entry point one `if (events is null)` is enough as a single guard, and `dependencyInjector` is *not* null-checked anyway despite the same pattern (`NavigationController.cs:39, :78`), revealing the inconsistency.

Even more telling: every public method that uses `stack`/`provider`/`transitions`/`middleware` re-asserts initialization (`NavigationController.cs:65, :90, :114`):

```csharp
if (stack == null || provider == null || transitions == null || middleware == null)
    throw new InvalidOperationException("NavigationController has not been initialized correctly.");
```

These four fields are private, set once in the constructor, and never reassigned. They cannot be null after the constructor exits. This is the "redundant guard clause" the rubric explicitly forbids — three times.

`NavigationStack.cs:22, :34, :45, :51, :57, :69, :79, :98, :112` does the same — `if (stack == null) throw new InvalidOperationException("Navigation stack is not initialized.");` on every method. `stack` is a private readonly field initialized inline. It cannot be null. Delete all of them.

### 4.6 `NavigationOptions.CloseAllViews bool?` legacy field

`NavigationOptions.cs:20`: `public bool? CloseAllViews;`. README §"Behavior Contracts" still documents it. `NavigationStackResolver.cs:27-35` still reads it. New code should use `StackPolicy = NavigationStackPolicy.ClearBelowCurrentAndPush`. This is partial migration. Either:

- Mark `[Obsolete("Use NavigationStackPolicy.ClearBelowCurrentAndPush", error: false)]`, then in a follow-up `error: true`, then delete.
- Or commit to the legacy field and delete the policy enum (don't).

Right now both exist with a translation layer, doubling the API surface. The architect will hate that.

Also: `NavigationOptions.RenderOverride` is a `RenderMode?` field (`:18`) — public mutable field, no encapsulation. Make immutable.

### 4.7 `INavigation.Open(controller, bool closeCurrent = false, NavigationOptions options = null)` overload conflict

`INavigation.cs:5-7`:

```csharp
void Open<TViewController>(TViewController controller, NavigationOptions options) where TViewController : IViewController;
void Open<TViewController>(TViewController controller, bool closeCurrent = false, NavigationOptions options = null) where TViewController : IViewController;
```

Two `Open` overloads that take different combinations and overlap when both `closeCurrent` and `options.StackPolicy = ReplaceCurrent` are set. Resolver code reconciles them (`NavigationStackResolver.cs:18-22`) but the public API has two ways to spell the same intent. Pick one — kill the `bool closeCurrent` parameter, force callers through `NavigationOptions` (or the `NavigationExtensions.OpenReplace<T>` helper).

### 4.8 `ServerNavigationController` — pure dead code

`ServerNavigationController.cs:12-75`. Every method is `GuardServerState() { if (this == null) throw }` then `return default`. `if (this == null)` is impossible inside a non-static instance method (well — for `MonoBehaviour` it is the destroyed-but-not-null overloaded `==`, so it has *some* meaning, but the throw fires on a destroyed component which is not what server logic implies). The class clearly meant to be a "server-side null impl" but does nothing useful. There are also methods (`Initialize`, `GetPreviousView`, `Open(Type, ...)`, `TryGetOpenView<T>`) that aren't even on `INavigation` — so this class doesn't even compile against the interface (it does, because those methods just don't override anything).

**Delete it.**

### 4.9 `NoView` — pure dead code

`Utility/NoView.cs:12-63`. Every member throws `NotImplementedException`. It's referenced in one place: `AnimationViewSchema.CheckContains` (`AnimationViewSchema.cs:45`) uses `typeof(NoView)` as a sentinel for "no target point." That is a marker-type abuse — use a distinct sentinel (`static readonly Type NoTargetPointSentinel = typeof(NavigationPoint);` or a dedicated `NoTarget` empty marker class). Right now `NoView` is a public-ish (internal but extant) `IView` that crashes if you ever instantiate it.

**Delete `NoView`, replace with a private marker.**

### 4.10 `IViewContext` / `IViewContextHost` — never referenced

`IViewContext.cs`, `IViewContextHost.cs`. No callers in this package. Probably leftover from a previous architecture. Either there is consumer code in another package using these (worth confirming) or they are dead. Delete or wire them.

### 4.11 `INavigationMiddleware` — empty marker interface

`INavigationMiddleware.cs:1-6`:

```csharp
public interface INavigationMiddleware { }
```

Empty marker. The only sub-interface is `INavigationOpenHandler` with a single method. The middleware concept is paper-thin: there is one extension point (`OnOpen`), no `OnClose`, no `OnReturn`, no chain-of-responsibility, no `next()` invocation. Calling this "middleware" oversells it. Either:

- Build out the middleware (close handler, return handler, error handler, before/after pairs).
- Or rename `INavigationOpenHandler` to `IOpenObserver` and delete `INavigationMiddleware` + the `OfType<INavigationOpenHandler>()` enumeration in `NavigationMiddleware.cs:20`.

### 4.12 `NavigationMiddleware.GuardHandlers` (`NavigationMiddleware.cs:34-40`)

```csharp
private void GuardHandlers()
{
    if (openHandlers == null)
        throw new System.InvalidOperationException("Open handlers are not initialized.");
}
```

`openHandlers` is assigned in the constructor (`NavigationMiddleware.cs:20`) and the constructor null-checks `middlewares`. `openHandlers` cannot be null. Dead guard.

### 4.13 `NavigationStack` — query API has four overloads

`NavigationStack.cs:20-53` — `Get<T>()`, `Get(Type)`, `Get(IView)`, `Get(IViewController)`. The first two are nominally redundant. Each does a `LastOrDefault` linear scan with a closure. Acceptable for stack sizes < 10, but the bigger smell is the surface area.

`Get<T>` has a hidden bug: `Type viewType = point.View.GetType();` (`:26`) — if `point.View` is null because the addressable hasn't materialized yet, this NREs. `NavigationPoint` exposes `IsReady` (`NavigationPoint.cs:54`) but `Get` doesn't check.

### 4.14 `NavigationTransitions.ExecuteDefaultTransitionCore` — semicolon-stuffed one-liners

`NavigationTransitions.cs:84-98`:

```csharp
private async Task ExecuteDefaultTransitionCore(ViewTransitionData transition)
{
    if (transition.From != null) HandleFromDefaultCore(transition);
    if (transition.To == null) return;
    var to = transition.To; var viewType = to.ViewModel.GetType();
    var beforeOpenEvent = new BeforeViewOpenEvent(viewType); events.Raise(beforeOpenEvent); to.View.gameObject.SetActive(true);
    if (to.View.State is ViewState.Closed) to.View.Bind(to.ViewModel);
    if (to?.Disposed != true && to?.View != null && to.View.gameObject != null)
    {
        if (to.View.State is ViewState.Open) to.View.Focus();
        if (to.View.State is not ViewState.Open) to.View.Open();
    }
    var afterOpenEvent = new AfterViewOpenEvent(viewType); events.Raise(afterOpenEvent);
    await Task.CompletedTask;
}
```

Multiple statements per line, defensive null checks against `to` (which was non-null three lines up), and `await Task.CompletedTask` to satisfy the `async` modifier. Either make it `Task` (not `async`) and `return Task.CompletedTask`, or actually await something. The `?.Disposed` chain is trying to handle the "view was disposed mid-transition" race — that race is real (the transition queue can run after `Dispose`), but the band-aid here is incomplete and the formatting is hostile.

`ApplyFromStateCore` (`:107-115`) repeats `if (transition.CloseCurrent)` three times in a row instead of one block.

### 4.15 `NavigationStack.UpdateCurrentPointAfterRemoval` indentation is broken

`NavigationStack.cs:88-93`:

```csharp
private void UpdateCurrentPointAfterRemoval(NavigationPoint point)
{
    if (CurrentPoint == point)
{
    CurrentPoint = stack.LastOrDefault();
}
}
```

The brace alignment is mid-edit garbage. Same in `RemoveFromStack` (`:78-86`) and elsewhere in `NavigationTransitions.cs`. Pure formatting hygiene — one auto-format pass cleans this up.

### 4.16 `NavigationController.Close<TViewController>(controller)` ignores generic

`NavigationController.cs:88-98`:

```csharp
public void Close<TViewController>(TViewController controller) where TViewController : IViewController
{
    // ...
    var point = this.stack.Get(controller);
    if (point == null)
    {
        Debug.LogWarning("Trying to close a view that is no longer in the stack");
        return;
    }
    ClosePoint(point);
}
```

The generic `<TViewController>` is unused — `stack.Get(IViewController)` takes the runtime instance. Drop the generic; takes `IViewController`. (Same pattern in `Open`: `Open<TController>(TController controller, ...)` is generic-on-instance which doesn't buy compile-time safety either; the controller→view mapping is the runtime dictionary lookup.)

Also, the `Debug.LogWarning` here is exactly the "default value that hides errors" anti-pattern. If a caller asks to close a view that isn't in the stack, that is a programming error, not a warning. Throw or at minimum funnel through an error handler.

### 4.17 `NavigationProvider.GuardConstructor` is a redundant guard helper

`NavigationProvider.cs:13`, `:57-62`:

```csharp
private void GuardConstructor(NavigationSettings settings, Transform viewHolder, IAddressablesGateway addressables)
{
    if (settings == null) throw new ArgumentNullException(nameof(settings));
    if (viewHolder == null) throw new ArgumentNullException(nameof(viewHolder));
    if (addressables == null) throw new ArgumentNullException(nameof(addressables));
}
```

Same null checks as the caller in `NavigationController` (which already null-checks `settings`, `viewHolder`, `addressablesGateway`). Both layers null-check the same three references because each was written defensively. Pick one boundary — the entry point (rubric: "Entry point only").

### 4.18 `Type.IsAssignableFrom` lookup is order-dependent

`NavigationSettings.cs:36`:

```csharp
ViewConfig config = screens.FirstOrDefault(s =>
    isController ? s.ControllerType.IsAssignableFrom(type) : s.ViewType.IsAssignableFrom(type));
```

`FirstOrDefault` returns the first config whose `ControllerType.IsAssignableFrom(type)` is true. If two configs have base/derived controller types, the order of the `screens` list determines the match. That is a serialized list in a ScriptableObject — which means whoever drags assets in last wins, and the bug is invisible in code review. Use `FirstOrDefault(s => s.ControllerType == type)` for exact match, fall back to `IsAssignableFrom` only with a deterministic priority (most-derived first), or detect ambiguity and throw.

### 4.19 `NavigationViewInstanceBuffer.Return` destroys silently when full

`NavigationViewInstanceBuffer.cs:62-67`:

```csharp
if (pool.Count >= maxPerView)
{
    UnityEngine.Object.Destroy(view.gameObject);
    return;
}
```

`Return` looks like a buffer return; it actually destroys the GameObject. Method name lies. Either rename to `ReturnOrDestroy`, or split into `TryReturn(view)`-returns-bool and let the caller `Destroy` if false (what `NavigationAssetHandleBuffer.Return` does — `:47-66`).

### 4.20 `NavigationProvider.FetchContextViews` mutates discovered views

`NavigationProvider.cs:47-55`:

```csharp
private void FetchContextViews()
{
    IView[] views = viewHolder.GetComponentsInChildren<IView>(true);
    foreach (IView view in views)
    {
        contextViews[view.GetType()] = view;
        view.gameObject.SetActive(false);
    }
}
```

Side-effecting deactivation in a method named "fetch". Also: if two scene-views share a `Type` (unlikely but possible), the second silently overwrites the first. Document the constraint or detect duplicates.

### 4.21 Newlines & file footers

Multiple files end with 4–6 blank lines (`INavigationMiddleware.cs`, `IViewController.cs`, `IViewContext.cs`, `NavigationController.cs`, `NavigationStack.cs`, etc.). Tooling scratch. Run a formatter pass.

### 4.22 `NavigationController.Open` does not call `dependencyInjector.Inject`

`NavigationController.cs:81-86`:

```csharp
private void Open(NavigationPoint point, bool closeCurrent, NavigationOptions options)
{
    middleware.OnOpen(point.ViewModel);
    point.ViewModel.Bind(this);
    GoTo(point, closeCurrent, options);
}
```

`PrepareDependencies` (`:71-79`) calls `dependencyInjector?.Inject(controller)` — but the regular `Open` flow runs `middleware.OnOpen(...)` which (because `NavigationInjection` registers as `INavigationOpenHandler` and `IViewControllerDependencyInjector` simultaneously, see `NavigationInjection.cs:15-18`) does the injection through `OnOpen → Inject`. So injection happens via the middleware chain, but only because the *same instance* is registered as both. This is implicit dependency: if a future install registers two different instances, `Open` no longer injects. Make it explicit:

```csharp
private void Open(NavigationPoint point, bool closeCurrent, NavigationOptions options)
{
    dependencyInjector.Inject(point.ViewModel);
    middleware.OnOpen(point.ViewModel);
    point.ViewModel.Bind(this);
    GoTo(point, closeCurrent, options);
}
```

Tests confirm the same-instance assumption (`NavigationInstallerAndInjectionTests.cs:51-65`). That's testing the implementation detail, not the contract. Make injection explicit and the test becomes "controller is injected before Bind," which is the actual contract.

### 4.23 `Samples/NavigationUseCases.cs` is a class with two methods, not a sample

`NavigationUseCases.cs:5-44`. Inner classes `SampleViewController` and `NullNavigation`. The methods compile and `Debug.Log`. There's no scene, no `MonoBehaviour`, no actual demo. Either ship a real sample (scene + assets) or delete this file. Right now it confuses adopters — they'll search for "Samples" and find a stub.

### 4.24 `NavigationAssetProvider` hard-codes the addressable key as a string

`NavigationAssetProvider.cs:13`:

```csharp
protected override AssetReference AssetKey => new AssetReference("Navigation Settings");
```

Stringly-typed addressable name. If somebody renames the asset or its address, this fails at runtime with "Addressable not found". Pair with a constants source generator (or use the `AssetReference` from a settings asset on `NavigationAssetProvider`).

---

## 5. Suggested before/after

### 5.1 Typed routes via source generator

**Before:**
```csharp
INavigation navigation = resolver.Resolve<INavigation>();
navigation.Open(new MainMenuViewController());
```

**After:**
```csharp
// Generated from [ViewRoute("MainMenu", typeof(MainMenuViewController))] attributes
// or from NavigationSettings asset content.
public static class Routes
{
    public static readonly Route<MainMenuViewController> MainMenu = new(typeof(MainMenuViewController));
    public static readonly Route<SettingsViewController>  Settings = new(typeof(SettingsViewController));
}

navigation.Open(Routes.MainMenu);                  // compile-time match
navigation.Open(Routes.Settings, NavigationOptions.ReplaceCurrent());
// navigation.Open(Routes.SettingX);               // compile error
```

Analyzer enforces: every `Route<T>` must have a corresponding `ViewConfig` asset (compile-time check by scanning the project's settings asset).

### 5.2 Real popup model

**Before:**
```csharp
public enum ViewType { Screen = 0, Popup = 1 }
```

…and one `if (to.View.Type is ViewType.Screen)` branch.

**After:** two separate stacks, with popups owned by a screen.

```csharp
public interface INavigation
{
    void OpenScreen<T>(T controller, ScreenOptions options = default) where T : IScreenController;
    void OpenPopup<T>(T controller, PopupOptions options = default) where T : IPopupController;
    bool DismissTopPopup();          // returns false if no popup open
    IViewController Return();        // pops screen
    bool HandleBack();               // popup first, then screen, return true if handled
}
```

Popup stack lives on top of the current screen, doesn't interact with the screen stack, and `HandleBack` makes the routing explicit. Popups can stack; closing the underlying screen closes its popup stack.

### 5.3 Lifecycle hooks

**Before:** `IViewController { void Bind(INavigation); void Close(); }`.

**After:**
```csharp
public interface IViewController : ILifecycleAware { }
public interface ILifecycleAware
{
    Task OnAppearingAsync(CancellationToken ct);
    Task OnDisappearingAsync(CancellationToken ct);
    void OnFocused();        // returned-to from a deeper screen
    void OnBlurred();        // a popup or new screen came on top
}
```

Async appearing/disappearing means a controller can `await` data load *before* the view is shown — much more useful than the current "view appears, controller raids the world for state."

### 5.4 Inline the redundant guards

**Before** (`NavigationController.cs:65, :90, :114`):
```csharp
if (stack == null || provider == null || transitions == null || middleware == null)
    throw new InvalidOperationException("NavigationController has not been initialized correctly.");
```

**After:**
```
[delete all three occurrences; private readonly fields, set in ctor, cannot be null]
```

Same for `NavigationStack.cs:22, :34, :45, :51, :57, :69, :79, :98, :112`, `NavigationMiddleware.cs:34-40`, `NavigationProvider.cs:57-62`. Easy 30-line deletion.

### 5.5 Replace `async void` queue drainer

**Before** (`NavigationTransitions.cs:38-46`):
```csharp
private async void RunTransitions()
{
    runningTransition = true;
    while (pendingTransitions.Count > 0) { await ProcessNextTransition(); }
    runningTransition = false;
}
```

**After:**
```csharp
private Task transitionLoop;

private void EnsureLoop()
{
    if (runningTransition) return;
    runningTransition = true;
    transitionLoop = RunTransitionsAsync();
}

private async Task RunTransitionsAsync()
{
    try
    {
        while (pendingTransitions.Count > 0) await ProcessNextTransition();
    }
    catch (Exception ex)
    {
        errorReporter.Report(nameof(NavigationTransitions), ex);
    }
    finally
    {
        runningTransition = false;
    }
}
```

(Inject `IAppFlowErrorHandler` or a navigation-local `INavigationErrorReporter`.)

### 5.6 Delete dead code

```
DELETE Runtime/Implementation/ServerNavigationController.cs
DELETE Runtime/Utility/NoView.cs                (replace sentinel use with private marker)
DELETE Runtime/Contracts/IViewContext.cs        (if no consumer in this repo)
DELETE Runtime/Contracts/IViewContextHost.cs    (same)
DELETE Runtime/Contracts/INavigationMiddleware.cs (collapse to INavigationOpenHandler renamed)
```

### 5.7 Drop the `closeCurrent` parameter

**Before:**
```csharp
void Open<T>(T controller, NavigationOptions options) where T : IViewController;
void Open<T>(T controller, bool closeCurrent = false, NavigationOptions options = null) where T : IViewController;
```

**After:**
```csharp
void Open<T>(T controller, NavigationOptions options = null) where T : IViewController;
// `closeCurrent=true` becomes options.StackPolicy = ReplaceCurrent
```

Update `NavigationStackResolver` accordingly; delete `NavigationOptions.CloseAllViews`.

---

## 6. Easy wins (5–8)

1. **Delete `ServerNavigationController.cs`, `NoView.cs`, `IViewContext.cs`, `IViewContextHost.cs`, `INavigationMiddleware.cs` (after renaming the open-handler interface)** — pure dead code.
2. **Strip the redundant `if (stack == null) ...` and `if (...is null) throw new InvalidOperationException("not initialized")` guards** in `NavigationController` (3 occurrences), `NavigationStack` (9 occurrences), `NavigationMiddleware` (1 occurrence), `NavigationProvider.GuardConstructor` (1 helper).
3. **Replace `async void RunTransitions`** in `NavigationTransitions.cs:38-46` with `async Task` + try/catch/finally.
4. **Mark `NavigationOptions.CloseAllViews` `[Obsolete]`** and update README to drop the "(legacy)" notes.
5. **Run a formatter** — `NavigationStack.cs:78-93` and `NavigationViewInstanceBuffer.cs` have broken indentation; `NavigationTransitions.cs:84-115` has multi-statement-per-line code.
6. **Make injection explicit in `NavigationController.Open`** — call `dependencyInjector.Inject(controller)` instead of relying on the same-instance `OnOpen` coincidence.
7. **Rename `NavigationViewInstanceBuffer.Return` → `ReturnOrDestroy`** (it destroys when the pool is full).
8. **Change `NavigationSettings.GetViewConfig` `Exception` to `KeyNotFoundException`** (or a domain-specific `NavigationRouteNotFoundException`) — `throw new Exception(...)` (`:39`) is the laziest possible exception choice.

---

## 7. Bigger refactors

### 7.1 Compile-time route safety

Generate `Routes.Foo` typed routes from a settings asset or attributes. Analyzer rejects `INavigation.Open` with a controller type that has no route. This is the rubric's central typing requirement.

### 7.2 Real popup separation

Two stacks (`screenStack`, `popupStack`), two open methods, separate close semantics, separate back-press behavior. Promote `ViewType` from a flag-on-screen to a fundamental routing distinction.

### 7.3 Lifecycle + back-press + deep-link

`ILifecycleAware` (async appearing/disappearing, focused/blurred). `INavigationBackHandler` central back-press router that consults popups first. `INavigationDeepLink { Task<bool> TryNavigateAsync(Uri uri, CancellationToken ct); }` overlay so the same nav system handles app-launch URLs and notifications.

### 7.4 Transition pipeline as middleware

Right now `NavigationTransitions.ExecuteDefaultTransitionCore` hard-codes the close/hide/open dance. Promote it to an `ITransitionStep[]` that an installer can reorder. Today schemas (`AnimationViewSchema`, `TransitionViewSchema`) already let you swap behavior per-view — but the *order* and *steps* are fixed. A pipeline-of-steps that applies to a single transition is the right shape if the architect wants to add things like "fade audio between transitions" or "pre-warm next-screen on hover."

### 7.5 Asset-handle / instance-buffer eviction policy

Currently `maxPerView = 2` per config. There is no global memory-pressure-driven eviction. For an app that opens 30 screens, that is 30 resident addressable handles. Make it observable (event when retained), pluggable (`IBufferPolicy`), and let the app decide.

---

## 8. Organization & docs

### Asmdef hygiene
- Container split is good: `Scaffold.Navigation` (runtime, no VContainer) + `Scaffold.Navigation.Container` (VContainer wiring). Keep it; copy to SceneFlow.
- `Scaffold.Navigation.Editor` correctly editor-only.
- `Scaffold.Navigation.Samples` exists but contains one stub (§4.23). Either ship a real sample scene or remove the asmdef.
- `Scaffold.Navigation.asmdef` references three GUIDs (likely `Scaffold.Types`, `Scaffold.Schemas`, `Scaffold.Records`) — by-name would be more readable, but GUIDs are stable. Acceptable.

### Documentation
- README is the longest of the three. Honest about complexity. The mermaid sequence diagram is helpful.
- README says "Behavior Contracts" table includes `(legacy when StackPolicy is Push)` — drop legacy when removing `CloseAllViews`.
- `package.json` declares deps cleanly; `com.scaffold.appflow`, `com.scaffold.types`, `com.scaffold.schemas`, `com.scaffold.events`, `com.unity.addressables`, `com.scaffold.addressables`. Self-consistent.
- No XML docs on public APIs (`INavigation`, `IView`, `IViewController`, `NavigationOptions`). The architect has source generators in the toolbox — XMLDoc on the public boundary is essential.

### Tests
- Two test files (`NavigationStackResolverTests.cs`, `NavigationInstallerAndInjectionTests.cs`). Good coverage on the resolver. Container tests verify the `same-instance` fact.
- Missing: `NavigationStack` tests (depth math has an off-by-one risk in `GetPointDepth` `:96-107`), `NavigationTransitions` tests (the queue drainer has the `async void` bug; tests would have caught a stalled queue), strategy-priority tests (`NavigationProvider` order), `ViewConfig.OnValidate` editor tests, popup vs screen layering tests (currently zero).
- No back-press tests (because there's no back-press to test).

### Naming & layout
- `Implementation/` is the catch-all for non-contract types — fine.
- `Contracts/` mixes interfaces, enums, records, and the marker interface. Acceptable.
- `Utility/NoView.cs` is in the wrong place; move sentinel concerns into `Implementation/`.
- `Providers/NavigationAssetProvider.cs` is one file in its own folder. Either fold into `Implementation/` or commit to a `Providers/` family.

### Sources / patterns to reference
- **MAUI Shell** — typed routes, `OnNavigatedTo`/`OnNavigatedFrom`, modal vs. push, deep-link query parameters. The closest mainstream design.
- **Android Jetpack Navigation Compose** — typed routes via sealed classes / kotlinx.serialization.
- **iOS UINavigationController + UIViewController** — `viewWillAppear`, `viewDidDisappear`, `viewWillLayoutSubviews`. Classic lifecycle granularity.
- **Cysharp/UniTask** — `UniTaskCompletionSource` for `NavigationPoint.AwaitReadyAsync` (`NavigationPoint.cs:57`) zero-alloc replacement. `UniTask.NextFrame` for transition pacing.
- **VContainer** — `ITickable` / `IInitializable` for view lifecycle if you want it driven by the container.
