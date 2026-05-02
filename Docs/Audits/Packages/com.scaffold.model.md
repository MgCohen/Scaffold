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
