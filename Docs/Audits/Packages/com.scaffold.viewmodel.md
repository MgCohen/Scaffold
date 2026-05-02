# Audit: `com.scaffold.viewmodel`

Audited: 2026-05-02. Reviewer: senior architect (audit pass).
Path: `Assets/Packages/com.scaffold.viewmodel`
Asmdef: `Scaffold.MVVM.ViewModel` (`noEngineReferences: false`).

## 1. Summary

Tiny package — 4 files. It exposes one base class (`ViewModel : ObservableObject, IViewModel`) and one contract (`IViewModel : IViewController`), and orchestrates the `Bind(INavigation)` lifecycle that resets bindings, registers nested observable properties, and ties child viewmodels into the navigation graph. Architecturally this is a sound seam — you keep navigation-coupling here and the engine-free binding contracts in `com.scaffold.mvvm`. **However the package violates its own boundary**: `ViewModel.cs` `using UnityEngine;` is dead-but-present, the asmdef sets `noEngineReferences: false`, and the public `Close()` method does a defensive `navigation == null` check that means the class can be in a half-bound state without throwing. There's also one missing override (`OnPropertyChanged` is `sealed` — that closes the door on derived viewmodels using CommunityToolkit's `[NotifyPropertyChangedFor]` if they ever need to intercept).

**Verdict: keep with light cleanup.** The shape is right; tighten the lifecycle invariants and remove the engine reference.

## 2. Structure

```text
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
- Generator-emitted code is invisible to consumers. Consider adding a snippet in the README showing what `RegisterNestedProperties` looks like after generation, so users understand the cost model. (Recommend a hidden link in the package README pointing to the generator authoring file at `Generators/MVVMCompositionGenerator/MVVMCompositionGenerator.cs`.)

## References

- CommunityToolkit.Mvvm `ObservableObject` perf: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observableobject
- `[RelayCommand]` and `IAsyncRelayCommand`: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/relaycommand
- ReactiveUI `WhenActivated` lifetime tokens: https://www.reactiveui.net/docs/handbook/when-activated/
- Avalonia ViewModel base + `IDisposable` lifecycle convention: https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/

## Consumers

Repo-wide grep across `Assets/`, `GameModule/`, `LiveOps/` for `using Scaffold.MVVM.ViewModel*`, `: ViewModel`, `[BindSource]` on a class deriving from `ViewModel`, and direct `BindChildViewModel<T>` calls. **Result: a single derivation outside the package.**

- `Assets/Packages/com.scaffold.view/Samples/MVVMUseCases.cs:73-86` — `public partial class SampleViewModel : ViewModel` is the only `: ViewModel` derivation in the repo. The class declares `[ObservableProperty] private BuildSampleModel sampleModel; [ObservableProperty] private int value;` and overrides `Initialize()` with `Bind(() => SampleModel.Value, () => Value)`. Smell visible from the call site: the override is mandatory boilerplate (every concrete VM that wants bindings must override `Initialize`), and the VM binds itself to itself (a property mirror) — that's the only documented usage shape.
- `Assets/Packages/com.scaffold.view/Samples/MVVMUseCases.cs:15-16, 24-25` — `viewModel.Bind(null)` is called twice in samples. The package's `Bind(INavigation)` accepts null (no `ArgumentNullException` on the `navigation` parameter — wait, audit §4.7 says it's there at `ViewModel.cs:24-27`; sample passes `null` and gets `ArgumentNullException` at runtime). The sample is **broken**: `Bind(null)` will throw immediately, so the sample never reaches `BuildRegisterNested`. This is dead documentation. Reinforces the audit's general thinness on lifecycle tests.
- `BindChildViewModel<T>` consumers: **zero outside the package's own test** (`Tests/ViewModelChildPrepareTests.cs`). The single test is the only field exercise of the navigation-prepare-then-bind ordering invariant.
- `[NestedObservableObject]` consumers above `ViewModel`: only `Model.cs` (`Assets/Packages/com.scaffold.model/Runtime/Model.cs:6`) — itself an empty class. So nested observable composition has zero shaped instances in production code. Audit §4.3 (rebind nested-handler leak) has no production exposure.
- `protected INavigation navigation` field reads from outside `ViewModel.cs`: **zero** (the sample doesn't use it). Audit §4.6 (mutable protected field) has no current attack surface but the bug is real.
- `OnClosed()` overrides: **zero outside the package.** The lifecycle hook is unused.
- `Initialize()` overrides: **one** — `SampleViewModel.Initialize` in the sample.
- `[RelayCommand]` consumers anywhere in the repo: **zero.** The CommunityToolkit DLL is shipped via the engine package but the command pattern is unused, exactly as the audit's R1 calls out. The View→VM channel today is `ViewEvents.Raise<TEvent>` (cross-package, in the View audit), not `ICommand`.

**Net read on the existing audit findings.** Single-consumer surface means:
- The `noEngineReferences: false` boundary leak (§4.1) is mechanically fixable today — flip the asmdef, delete `using UnityEngine;`, no consumer rebuilds break.
- `Close()` silent no-op (§4.2) and the `Initialize`-vs-rebind nested-handler leak (§4.3) have no production callers exercising them.
- The sealed `OnPropertyChanged` (§4.4) constraint has not bitten anyone because no derived VM has needed cross-cutting INPC interception yet.
- The sample passes `null` to `Bind` and immediately throws (per §4.7) — **the only ViewModel sample in the repo is broken**. This is the most consequential finding here: the documented happy path doesn't run.

## Alternatives & prior art

| Library / pattern | Description | Verdict | Rationale |
|---|---|---|---|
| **CommunityToolkit.Mvvm `[RelayCommand]`** ([docs](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/relaycommand)) — DLL already shipped via `com.scaffold.mvvm/GeneratorsMVVM/Community/` | Source-generated `IRelayCommand`/`IAsyncRelayCommand` per method, with `CanExecute` and async support. | **Adopt.** | Closes the missing command path called out in audit §7 R1. `ViewModel` already inherits `ObservableObject`; adding `[RelayCommand]` to a method emits `IAsyncRelayCommand SubmitCommand` automatically. Replaces `ViewEvents`-routed UI clicks for view-local actions. |
| **CommunityToolkit.Mvvm `[NotifyPropertyChangedFor]`/`[NotifyCanExecuteChangedFor]`** ([docs](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observableproperty#notifying-dependent-properties)) | Declarative dependent-property notifications. | **Adopt.** | The current pattern (`Bind(() => SampleModel.Value, () => Value)` in `Initialize` to mirror a model field into a VM property) is *exactly* the case `[NotifyPropertyChangedFor]` solves declaratively. Reduces `Initialize` boilerplate and removes a string-keyed dispatch path. |
| **ReactiveUI `WhenActivated` + `IActivatableViewModel`** ([docs](https://www.reactiveui.net/docs/handbook/when-activated/)) | Lifetime tokens issued at activate, disposed at deactivate; consumers `.DisposeWith(disposables)`. | **Steal pattern.** | Direct fit for audit §7 R3 (CancellationToken on `OnClosed`). `ViewModel.Bind` issues a `CancellationToken Lifetime`, cancels in `Close`. Async `Initialize` overloads naturally tie to that token; bound `IDisposable`s `.DisposeWith(...)` automatically. |
| **Avalonia `ViewModelBase : ReactiveObject, IDisposable`** ([docs](https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/)) | Standard `IDisposable` on the base; lifecycle is explicit. | **Adopt.** | Address audit §4.8: extend `IViewModel : IDisposable`, default `Dispose()` calls `ClearBindings()` + `OnClosed()`. Today there is no contract for resource teardown outside `Close()`. |
| **R3 `CompositeDisposable.AddTo(this)`** ([github.com/Cysharp/R3](https://github.com/Cysharp/R3)) | Subscriptions registered with a disposable bag; bag disposes on `Close`. | **Wrap.** | Drop-in replacement for the manual subscribe/unsubscribe shape on `INotifyPropertyChanged`. Pairs naturally with the lifetime-token model (above). |
| **`CommunityToolkit.Mvvm.Messaging.IMessenger`** ([docs](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/messenger)) | DI-friendly weak-referenced message bus shipped in the same toolkit. | **Steal pattern.** | Alternative to MessagePipe for VM-to-VM communication that doesn't drag in VContainer dependency. Already in the shipped DLL; zero added dependency cost. |

## Benchmark plan

- **`ViewModel.OnPropertyChanged → UpdateBinding(string)` dispatch cost.** Measure ns/op and alloc bytes for `OnPropertyChanged(nameof(X))` on a VM with 1 / 5 / 20 `[ObservableProperty]` fields. Tool: `Unity.PerformanceTesting` EditMode (no scene needed). Location: `Assets/Packages/com.scaffold.viewmodel/Tests/PerfViewModelDispatch.cs` (new). Baseline: `UpdateBinding(e.PropertyName)` looks up by string in `BindGroups`. Success after `(Type, name) → int` keying (mvvm audit R3): zero alloc, halve the lookup time.
- **`Bind` → `Initialize` → `RegisterNestedProperties` end-to-end cost.** Measure first-bind and rebind times for VMs with 0 / 1 / 5 nested observable children. Tool: `Unity.PerformanceTesting`. Location: `Assets/Packages/com.scaffold.viewmodel/Tests/PerfViewModelBind.cs`. Baseline: rebind clears and re-runs nested-handler registration; today the generator's `__nestedRefreshHandlerRegistered` flag avoids double-registration but child-property handlers detach by reference (audit §4.3). Success: rebind cost ≤ 2× first-bind cost; no leaked subscriptions across 1 / 10 / 100 cycles (correctness assertion via `INotifyPropertyChanged` invocation count).
- **Rebind nested-handler leak — *write the test that proves it*.** EditMode test: VM with `[NestedObservableObject]` child; `Bind(navStub)`; replace nested model **outside** the `[ObservableProperty]` setter via reflection or backdoor field; `Bind(navStub)` again; assert that the prior nested model has zero `PropertyChanged` subscribers. Tool: NUnit. Location: `Assets/Packages/com.scaffold.viewmodel/Tests/NestedRebindLeakTests.cs` (new). Today this should fail per §4.3.
- **`Close` before `Bind` regression.** Correctness test: instantiate VM, call `Close()`, assert `InvalidOperationException` (post-fix). Today silently no-ops. Location: `Assets/Packages/com.scaffold.viewmodel/Tests/ViewModelCloseFailFastTests.cs` (new). 4-line test; pairs with the audit §6 easy win.
- **Generator output size for the ViewModel partial.** Measure: emitted `_nested.g.cs` and `_bindings.g.cs` byte counts and method counts for VMs with 1 / 5 / 20 properties and 0 / 1 / 5 levels of `ViewModel`-inheritance. Tool: `Microsoft.CodeAnalysis.Testing` (`CSharpSourceGeneratorTest`) snapshot tests. Location: `Generators/MVVMCompositionGenerator.Tests/ViewModelEmissionTests.cs` (new). Baseline: snapshot today's output (notably the per-derivation `base.RegisterNestedProperties()` duplication, mvvm audit §4.16). Success after capping emission to classes that introduce tracked fields: byte count drops linearly with the chain length saved.
- **`is INestedObservableProperties` cast cost.** Micro-bench (`BenchmarkDotNet`-shaped or `Unity.PerformanceTesting`) for the cast in `ViewModel.cs:30-33`. Location: `Assets/Packages/com.scaffold.viewmodel/Tests/PerfNestedCastRemoval.cs`. Baseline: a single virtual interface cast per `Bind`. Success: drop is small (1-2 ns) but provable — confirms §4.5 is a cleanup, not a perf fix.
