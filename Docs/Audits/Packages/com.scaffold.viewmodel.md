# Audit: `com.scaffold.viewmodel`

Audited: 2026-05-02. Reviewer: senior architect (audit pass).
Path: `/home/user/Scaffold/Assets/Packages/com.scaffold.viewmodel`
Asmdef: `Scaffold.MVVM.ViewModel` (`noEngineReferences: false`).

## 1. Summary

Tiny package — 4 files. It exposes one base class (`ViewModel : ObservableObject, IViewModel`) and one contract (`IViewModel : IViewController`), and orchestrates the `Bind(INavigation)` lifecycle that resets bindings, registers nested observable properties, and ties child viewmodels into the navigation graph. Architecturally this is a sound seam — you keep navigation-coupling here and the engine-free binding contracts in `com.scaffold.mvvm`. **However the package violates its own boundary**: `ViewModel.cs` `using UnityEngine;` is dead-but-present, the asmdef sets `noEngineReferences: false`, and the public `Close()` method does a defensive `navigation == null` check that means the class can be in a half-bound state without throwing. There's also one missing override (`OnPropertyChanged` is `sealed` — that closes the door on derived viewmodels using CommunityToolkit's `[NotifyPropertyChangedFor]` if they ever need to intercept).

**Verdict: keep with light cleanup.** The shape is right; tighten the lifecycle invariants and remove the engine reference.

## 2. Structure

```
com.scaffold.viewmodel/
├── Runtime/
│   ├── AssemblyInfo.cs                   # InternalsVisibleTo Scaffold.MVVM.ViewModel.Tests
│   ├── Contracts/
│   │   └── IViewModel.cs                 # 1 interface, IViewModel : IViewController
│   ├── ViewModel.cs                      # 75 lines, the entire base class
│   └── Scaffold.MVVM.ViewModel.asmdef    # noEngineReferences: false (wrong)
├── Tests/
│   ├── Scaffold.MVVM.ViewModel.Tests.asmdef
│   └── ViewModelChildPrepareTests.cs     # 1 test (PrepareDependencies-before-bind)
├── README.md
└── package.json                          # deps: navigation + mvvm
```

Asmdef references: `Scaffold.MVVM`, `Scaffold.Navigation`. Precompiled refs: `CommunityToolkit.Mvvm.dll`, `System.Runtime.CompilerServices.Unsafe.dll`. `overrideReferences: true`. `autoReferenced: true`.

Tests: 1 case verifying `INavigation.PrepareDependencies` runs before the child's `Bind`. No tests for: Bind→Close→Bind cycle, double-Close, Close-without-Bind (which is the existing `null`-check path), nested observable registration, rebind clearing prior subscriptions.

## 3. What's good

- **Inherits `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`** rather than hand-rolling INPC. Setters from `[ObservableProperty]` source-gen avoid boxing for value types via `EqualityComparer<T>.Default.Equals`. This is the right call (compare to https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observableproperty).
- **Source-gen-driven bind facade**. The `[BindSource(typeof(TreeBinding))]` attribute (`ViewModel.cs:17`) plus `[NestedObservableObject]` (line 16) cause `MVVMCompositionGenerator` to emit the `Bind / BindCollection / UpdateBinding / ClearBindings / RegisterNestedProperties` machinery as a partial class. Consumers write almost no boilerplate. This is the architect's "minimum code, maximum extensibility" preference, executed correctly.
- **Lifecycle reset is centralized**. `ViewModel.Bind(INavigation)` (`ViewModel.cs:22-35`) calls `ClearBindings()` first, then re-initializes. So rebinding is safe by construction — you cannot accidentally double-subscribe by calling `Bind` twice.
- **Child viewmodels go through navigation**. `BindChildViewModel<T>` (`ViewModel.cs:42-52`) calls `navigation?.PrepareDependencies(viewModel)` so VContainer-wired dependencies on the child are populated before bind. The single test (`ViewModelChildPrepareTests.cs`) covers exactly this. Good seam, exercised.
- **Generic and constrained**. `BindChildViewModel<T> where T : IViewModel` (`ViewModel.cs:42`) preserves the concrete type for the caller. No `(IViewModel)cast` round-trip.
- **`OnPropertyChanged` override routes through `UpdateBinding`** (`ViewModel.cs:54-58`). This is the integration point with the binding engine — every INPC notification re-fires bound targets.

## 4. Issues / smells

### 4.1 Engine reference for no reason

`Runtime/ViewModel.cs:7`: `using UnityEngine;` is present but **nothing in the file uses any `UnityEngine` symbol**. Also `Runtime/Scaffold.MVVM.ViewModel.asmdef` declares `"noEngineReferences": false`. The README explicitly says (line 18, line 129):

> Forbidden Dependencies: Unity view lifecycle classes (`MonoBehaviour` view concerns).

Engine reference enabled at the asmdef level transitively allows engine drift later. Remove the using and flip `noEngineReferences: true`. The architect's stated preference is "Pure C# at separate boundaries when possible. ViewModels should be pure C#." This is the place to enforce it.

### 4.2 `Close()` silently no-ops if navigation is null

`Runtime/ViewModel.cs:60-68`:

```csharp
public void Close()
{
    if (navigation == null)
    {
        return;
    }
    navigation.Close(this);
    OnClosed();
}
```

Calling `Close` on a viewmodel that has never been bound returns silently. That violates fail-fast: the only way you can be in this state is a bug in the caller (closing a VM you never opened). Throw `InvalidOperationException("ViewModel must be bound before Close.")`. Also note `OnClosed()` is **not called** when navigation is null — so consumer cleanup never runs in that path either, asymmetric and surprising.

### 4.3 `Initialize` is not called on rebind if the consumer skips `Bind` second time

`Bind(INavigation)` is the only entry point that calls `Initialize`. There's no separate `Reinitialize`. If a downstream pattern wants to refresh bindings on a navigation context change without changing the navigation reference, they'd have to call `Bind(navigation)` again. That works (it clears bindings and re-runs `Initialize`), but it also re-runs `RegisterNestedProperties()`, which inside the generator (`MVVMCompositionGenerator.cs:99-108`) hooks `PropertyChanged` — and the generator's nested-refresh handler protects against double-registration with a `__nestedRefreshHandlerRegistered` flag, but the *child-property* handlers in `RefreshChildProperty` do `propertyChangedHandler -= old; propertyChangedHandler = new;` — so they detach by reference. This means rebinding **leaks zero subscriptions only if every nested object is the same reference as before**. If a nested model was replaced and never observed via `RefreshChildProperty` (e.g. assigned outside of `[ObservableProperty]`'s setter), the previous handler stays subscribed. Edge case, but real.

### 4.4 `OnPropertyChanged` is `sealed`

`Runtime/ViewModel.cs:54`: `protected sealed override void OnPropertyChanged(PropertyChangedEventArgs e)`. CommunityToolkit's idiom is to allow per-property override via `OnXxxChanging` / `OnXxxChanged` partial methods, *not* the base `OnPropertyChanged`, so sealing is mostly fine. But it means a downstream VM cannot intercept (e.g. for telemetry) without going through `PropertyChanged += handler`, which the SCM003 analyzer (`Generators/Scaffold.Mvvm.Analyzers/MvvmBindApiAnalyzer.cs`) flags. Net: there's no escape hatch for cross-cutting INPC concerns. Document the rationale or expose a `protected virtual void OnAfterPropertyChanged(PropertyChangedEventArgs e)` template-method hook.

### 4.5 `INestedObservableProperties` runtime check is unnecessary

`Runtime/ViewModel.cs:30-33`:

```csharp
if (this is INestedObservableProperties nestedObservableProperties)
{
    nestedObservableProperties.RegisterNestedProperties();
}
```

The source generator emits `INestedObservableProperties` on every `[NestedObservableObject]`-decorated class. `ViewModel` itself is decorated with `[NestedObservableObject]` (line 16), so `this is INestedObservableProperties` is **always true** — the generator made it so. The runtime cast is dead defensive code. Replace with a direct call (the method is available on the partial class). It also introduces a single is-cast in the hot path of every Bind.

### 4.6 `protected INavigation navigation;` is exposed mutably

`ViewModel.cs:20`: protected field, no `readonly`, set in `Bind`. Subclasses can stomp it. Should be `protected INavigation Navigation { get; private set; }` or kept private with a protected accessor. Right now nothing prevents a derived `Initialize()` from overwriting `navigation` and breaking `Close()`.

### 4.7 Redundant `ArgumentNullException` checks

`ViewModel.cs:24-27` and `:44-47`: explicit null guards on entry. Both are at *the* entry point of the package's only public method, so these are correct (architect's rule — guard at entry, not internally). But `BindChildViewModel` is `protected`, called only by derived viewmodels in code you control; the `ArgumentNullException` on a child VM that the developer just wrote is borderline noise. Keep `Bind`'s guard, drop `BindChildViewModel`'s — let the NRE surface naturally during `navigation?.PrepareDependencies(viewModel)` (it'd throw at `viewModel.Bind` instead).

### 4.8 No `IDisposable` on `IViewModel` / `ViewModel`

The generator-emitted `ClearBindings()` is the cleanup primitive but it's not exposed as `Dispose`. If a viewmodel holds non-binding resources (timers, cancellation tokens, services that need explicit teardown), there's no contract for it. `IViewModel` should extend `IDisposable` (or add an explicit `OnDestroyed` template method). Today the only teardown hook is `OnClosed()` which is invoked from `Close()` — *not* from a navigation-driven dispose. If the navigation system ever decides to Dispose without going through `Close`, your `OnClosed` never fires and your bindings leak.

### 4.9 `using` directives include unused namespaces

`ViewModel.cs:1-13`:

```csharp
using System.Linq.Expressions;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Collections;
...
using System.Linq;
using System.Collections.Generic;
```

None of these are used in the file as written. Source-gen output may pull them, but the partial-class file generated separately gets its own usings. Trim.

### 4.10 Test coverage is thin

One test verifies `PrepareDependencies` ordering. Missing: `Close` without `Bind` (the silent no-op path), `Close` after `Bind`, double-`Bind` clears stale registrations, derived `Initialize` is run, `OnClosed` is called.

## 5. Suggested before/after

### Fail-fast in `Close`

**Before** (`ViewModel.cs:60-68`):

```csharp
public void Close()
{
    if (navigation == null)
    {
        return;
    }
    navigation.Close(this);
    OnClosed();
}
```

**After:**

```csharp
public void Close()
{
    if (navigation is null)
    {
        throw new InvalidOperationException(
            $"{GetType().Name}.Close called before Bind(INavigation).");
    }
    navigation.Close(this);
    OnClosed();
}
```

### Drop the engine using and tighten the asmdef

**Before** (`Scaffold.MVVM.ViewModel.asmdef`):

```json
"noEngineReferences": false
```

**After:**

```json
"noEngineReferences": true
```

And in `ViewModel.cs:7`, delete `using UnityEngine;` along with the other unused usings.

### Drop the dead is-cast

**Before** (`ViewModel.cs:30-33`):

```csharp
if (this is INestedObservableProperties nestedObservableProperties)
{
    nestedObservableProperties.RegisterNestedProperties();
}
```

**After:**

```csharp
RegisterNestedProperties();   // partial class always implements INestedObservableProperties via [NestedObservableObject]
```

(Requires the method to be accessible — `MVVMCompositionGenerator.cs:94` emits `public virtual void RegisterNestedProperties()`, so a direct call is valid in the partial.)

### Make navigation read-only inside derived classes

```csharp
protected INavigation Navigation { get; private set; }
```

And replace internal references to `this.navigation`. One-line change.

## 6. Easy wins (each <30 min)

1. Delete unused `using` directives at top of `ViewModel.cs:1-13`.
2. Flip asmdef `noEngineReferences` to `true` in `Scaffold.MVVM.ViewModel.asmdef:27`.
3. Replace the `is INestedObservableProperties` cast in `ViewModel.cs:30-33` with a direct method call.
4. Throw on `Close()` before `Bind()` (`ViewModel.cs:60-68`).
5. Convert `protected INavigation navigation` to `protected INavigation Navigation { get; private set; }`.
6. Add 3 tests: `Close_BeforeBind_Throws`, `Bind_Twice_ClearsPriorRegistrations`, `Close_AfterBind_InvokesOnClosed`.
7. Add XML doc comments to `Bind`, `Close`, `BindChildViewModel`, `Initialize`, `OnClosed` — these are the public lifecycle surface and are undocumented.
8. Consider adding `IViewModel : IDisposable` or an explicit `IAsyncDisposable` if any subclass holds async resources.

## 7. Bigger refactors

### R1. Separate "model owns commands" from "viewmodel owns navigation"

The architect's brief asks: how are commands wired? Answer: **they aren't, in this package.** There's no `IRelayCommand`, no `ICommand`, no `IAsyncRelayCommand` exposed. CommunityToolkit.Mvvm ships `[RelayCommand]` and `IAsyncRelayCommand` (https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/relaycommand) but the precompiled DLL is referenced and unused for commanding. Concretely: how does a button-click on a View invoke a method on a ViewModel? Looking at `com.scaffold.view`, the answer is `ViewEvents.Raise<TEvent>(...)` — a separate event bus. That's a deliberate architectural choice (Unity-side input → typed event), but it bypasses the standard MVVM command pattern entirely. Either:

- Adopt `[RelayCommand]` for cross-cutting cases (cancellation, async, CanExecute) and route Unity input → command.
- Document in the README that commands are intentionally not used; events are the wire.

The current state — DLL imported, no usage — confuses readers.

### R2. Make ViewModel non-abstract or expose the lifecycle as composable services

`ViewModel` is `abstract` but contains zero abstract members. The reason it's abstract is convention. There's no cost to making it instantiable for tests/scaffolding — and the existing `private sealed class ChildVm : ViewModel { }` in tests already does the workaround.

### R3. Surface a CancellationToken for `OnClosed`

Async viewmodels (loading content, async services) need a cancellation token tied to view lifetime. Today there's nothing. Adding a `CancellationToken Lifetime { get; }` linked to `Bind`/`Close` is the correct pattern (see ReactiveUI's `WhenActivated`: https://www.reactiveui.net/docs/handbook/when-activated/, or R3's lifetime tokens).

## 8. Organization & docs

- README is in good shape and matches the project standard. The doc states the package's responsibilities clearly.
- The README's "Forbidden Dependencies" line (line 129) lists `MonoBehaviour` view concerns, but the asmdef's `noEngineReferences: false` doesn't *forbid* it — it permits it. Either the README is aspirational or the asmdef is wrong. Make them match.
- The `BindChildViewModel<T>` example in README works but uses `() => model.Value, () => Value` — this is a viewmodel binding *to itself* via `TreeBinding`, which is the in-VM data-flow case. The README doesn't show a child viewmodel actually being bound (which is what the test covers); add an example for that path.
- Generator-emitted code is invisible to consumers. Consider adding a snippet in the README showing what `RegisterNestedProperties` looks like after generation, so users understand the cost model. (Recommend a hidden link in the package README pointing to the generator authoring file at `/home/user/Scaffold/Generators/MVVMCompositionGenerator/MVVMCompositionGenerator.cs`.)

## References

- CommunityToolkit.Mvvm `ObservableObject` perf: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observableobject
- `[RelayCommand]` and `IAsyncRelayCommand`: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/relaycommand
- ReactiveUI `WhenActivated` lifetime tokens: https://www.reactiveui.net/docs/handbook/when-activated/
- Avalonia ViewModel base + `IDisposable` lifecycle convention: https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/
