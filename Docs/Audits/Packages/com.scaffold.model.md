# com.scaffold.model — Audit

## Summary
A two-line `Model` base class that is `[NestedObservableObject] partial class Model : ObservableObject` over CommunityToolkit.Mvvm. The package's reason to exist is to be the shared `Model` root for the project's MVVM. Asmdef wiring is correct (engine-free, source-generator-friendly), but the package is so thin that any project rule it carries lives implicitly in the source generator and the consuming style guide rather than here. **Verdict: keep, but it's barely a package; document or merge.**

## Structure
```
com.scaffold.model/
  Runtime/
    Model.cs                                 (8 LOC)
    Scaffold.MVVM.Model.asmdef               (engine-free, references CommunityToolkit.Mvvm.dll)
  Tests/Scaffold.MVVM.Model.Tests.asmdef     (no .cs)
  package.json, README.md
```
Asmdef name `Scaffold.MVVM.Model` and rootNamespace `Scaffold.MVVM` (`Runtime/Scaffold.MVVM.Model.asmdef:2-3`). Depends on `Scaffold.MVVM` assembly (the `Scaffold.MVVM.Binding.NestedObservableObject` attribute) and the precompiled `CommunityToolkit.Mvvm.dll` + `System.Runtime.CompilerServices.Unsafe.dll`.

## What's good
- `noEngineReferences: true` (`Scaffold.MVVM.Model.asmdef:26`) — Models are correctly Unity-free. This is the boundary the rubric asks for.
- `partial` (`Runtime/Model.cs:7`) — required for source-gen `[ObservableProperty]` patterns.
- Single, named entry point for "this is a Scaffold model" — derived classes get `INotifyPropertyChanged` plus the project's nested-observable behavior consistently. That's the right abstraction *if* it's enforced project-wide.
- Tests asmdef scaffolded.

## Issues / smells

### Existence justification
- The class adds **zero behavior** beyond what `[NestedObservableObject] partial class Foo : ObservableObject` already gives. Any consumer can just inherit `ObservableObject` directly (or paste the attribute). The package's value is purely "use this as the canonical project base class."
- If that *is* the value, an analyzer rule (`SCA…`) or an `[ObservableObject]`-style stamp from your generator would enforce it. Right now it's a soft convention with a single-class package.

### Test scaffold without tests
- `Tests/Scaffold.MVVM.Model.Tests.asmdef` exists with no `.cs`. Same pattern as the other packages.
- A Model base does have testable surface: `OnPropertyChanged` propagation through nested observables. Worth one or two tests.

### Naming inconsistency
- Package id `com.scaffold.model`, displayName `Scaffold MVVM Model`, asmdef `Scaffold.MVVM.Model`, rootNamespace `Scaffold.MVVM`. Three naming styles. Pick one (`Scaffold.MVVM.Model` everywhere is cleanest) and update package.json + folder name to match (`com.scaffold.mvvm.model`) so the namespace tells you the package.

### Container
- No `Container/` folder. That's fine — Models are POCOs, not registered DI services. Confirm and document.

### `Scaffold.MVVM` dependency
- Runtime references `Scaffold.MVVM` for the `[NestedObservableObject]` attribute (`Runtime/Model.cs:2`). Verify that the **attribute** lives in the engine-free part of `Scaffold.MVVM`, otherwise this package transitively pulls UnityEngine via its source-generator runtime. Quick check: open `Scaffold.MVVM.Binding.NestedObservableObject` and ensure the asmdef there is also engine-free.
- If `Scaffold.MVVM` isn't engine-free, this package is *claimed* engine-free but transitively isn't — a boundary leak the rubric specifically calls out.

## Suggested before/after

**Make it earn its keep.** Either…

A) **Add behavior worth importing.** Common base for command initialization, model lifecycle hooks, equality semantics, etc.:
```csharp
[NestedObservableObject]
public abstract partial class Model : ObservableObject
{
    protected Model() { OnInitialized(); }
    protected virtual void OnInitialized() { }
    // optional: standardized validation, dispatcher, etc.
}
```

B) **…or merge into `com.scaffold.mvvm`** and delete this package. A six-line type doesn't need its own UPM unit; the cost (extra asmdef compile, extra `package.json`, extra README to keep current) outweighs the value.

## Easy wins
1. Decide A or B above — it's a 30-min decision.
2. Add the missing `Tests/*.cs` with one nested-observable propagation test.
3. Align name: rename folder/package to `com.scaffold.mvvm.model`, asmdef stays `Scaffold.MVVM.Model`. Saves future readers from a triple-naming hunt.
4. Verify `Scaffold.MVVM.Binding` source asmdef is engine-free; if not, file a follow-up.
5. README needs a one-line rule: "every Scaffold viewmodel/model derives from `Scaffold.MVVM.Model`". Otherwise the package is invisible policy.

## Organization & docs
- This is the canonical place for an analyzer rule under `Analyzers/Rules/` to enforce "models inherit `Model`". Worth a SCA diagnostic if you keep the package.
- README at root: confirm it documents the source generator behavior contributed by `[NestedObservableObject]`, since users can't infer it from the 8-line file.
- If you keep it as the "official" Model root, also add a `View` and `ViewModel` companion convention so MVVM is symmetrical at package granularity (and consider whether com.scaffold.viewmodel is doing exactly that already — cross-reference).

## Consumers

Single consumer in `Assets/`: one sample file. **Zero non-sample consumers.**

**`com.scaffold.view/Samples/MVVMUseCases.cs:67`**:
```csharp
public partial class BuildSampleModel : Model
{
    [ObservableProperty]
    private int value;
}
```
This is the *only* `: Model` inheritance in `Assets/`. Smell: a sample, not a real consumer. The `BuildSampleModel` exists to demonstrate `Scaffold.View` MVVM, not to validate `com.scaffold.model`'s value as a separate package. Production code in this repo gets `INotifyPropertyChanged` via `ObservableObject` directly (or via `ViewModel`, which lives in `com.scaffold.viewmodel`).

**Verification:** zero references to `Scaffold.MVVM.Model`, no `: Model` inheritance, no asmdef dependencies from any non-sample asmdef. The `Scaffold.MVVM` (binding) namespace is heavily used — but that's a different package.

The audit's hypothesis ("the package's value is purely 'use this as the canonical project base class'") is contradicted by the consumer evidence: **the canonical project does not actually use it.** The sample alone justifies neither the package nor the asmdef.

This package is a one-line type that has not earned a single production inheritance. Either:
1. The audit's option B (merge into `com.scaffold.mvvm`, delete this package) is correct — there is nothing here to keep.
2. There's a hidden expectation that future code will derive from `Model`, in which case the project lacks an analyzer rule to enforce it (audit also flagged this).

The consumer side adds new evidence the audit didn't have: even the single sample doesn't depend on any `Model`-specific behavior. `BuildSampleModel : ObservableObject` would compile and behave identically. The package is invisible to its only consumer.

## Alternatives & prior art

- **`CommunityToolkit.Mvvm.ComponentModel.ObservableObject`** — already a dependency, ships `INotifyPropertyChanged` + source-gen `[ObservableProperty]`. `https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observableobject`. **Adopt directly**: the `Model` base adds zero behavior; consumers can derive from `ObservableObject` and stamp `[NestedObservableObject]` themselves (one attribute vs one base class — not a meaningful win).
- **`Prism.Mvvm.BindableBase`** — Prism's equivalent. `https://github.com/PrismLibrary/Prism/blob/master/src/Prism.Core/Mvvm/BindableBase.cs`. **Build (rejected)**: not relevant; the project chose CommunityToolkit, don't mix.
- **ReactiveUI `ReactiveObject`** — observable-driven. `https://www.reactiveui.net/docs/handbook/view-models/`. **Steal pattern** for the audit's option A "add behavior worth importing" — `ReactiveObject` has `WhenAnyValue` + `RaiseAndSetIfChanged` patterns that justify a base class. CommunityToolkit doesn't, so neither does `Model`.
- **Roslyn analyzer enforcing inheritance** — the audit suggested a `SCA…` rule. `https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix`. **Steal pattern**: even if the package is kept, the rule belongs in the project's analyzer asmdef, not in `com.scaffold.model`. Without the analyzer, the package is convention-only and consumers ignore it (proven above).

Verdict: **delete or merge.** The "single named entry point for a Scaffold model" concept has zero adopters in `Assets/`. Carry-forward cost (asmdef compile, README, tests folder, package.json, Unity Package Manager surface) is the only measurable thing this package produces today.

## Benchmark plan

The class adds no measurable runtime behavior over `ObservableObject`. There is nothing to micro-benchmark in isolation. The relevant tests are correctness:

- **`[NestedObservableObject]` propagation correctness**
  - What: changing a property on a nested `Model` raises `PropertyChanged` on the parent.
  - Tool: NUnit (`Tests/Scaffold.MVVM.Model.Tests.asmdef` already exists, no .cs).
  - Location: `Tests/ModelNestedPropagationTests.cs`.
  - Scenario: parent `Model` with a `Model` property; subscribe; mutate inner; assert outer fires.
  - Baseline: without the attribute, only the inner fires. With it, both fire (or path-aware notification, depending on source-gen behavior).
  - Success: deterministic propagation; one notification per change, no duplicates. Run as a regression guard if the package is kept.

- **Source generator overhead at compile time**
  - What: incremental compile time delta when a project uses `Model` vs `ObservableObject` directly.
  - Tool: `dotnet build /bl` + `MSBuild Binary Log Viewer`, or `Time-Command` in Unity's compile pipeline.
  - Location: not a test — a one-shot measurement, document in README.
  - Scenario: 50 model classes, both shapes.
  - Baseline: CommunityToolkit source-gen ≈ 50–200 ms per assembly cold; `[NestedObservableObject]` adds another generator pass.
  - Success: ≤ 10% overhead vs plain `ObservableObject`. If higher, the package's "convenience base" is a compile-time tax with zero adopters.

If the package is merged/deleted (recommended), neither of these benchmarks is needed.
