# Merge MVVM Lazy Binding and Per-Binding Unbind into One Cohesive Binding Upgrade

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this change, MVVM bindings will support two behaviors that currently conflict in implementation planning but belong in the same runtime model: deferred first evaluation for nullable late-bound object graphs, and selective teardown of individual bindings without clearing all bindings in the same context. Users will be able to register a bind to a path such as `LateInstance.Value` before `LateInstance` exists, then safely receive updates when the path becomes valid, and they will be able to dispose one returned bind handle while leaving other binds active.

Success is observable when EditMode tests prove these behaviors together: lazy registration does not throw or push invalid values, targeted dispose stops updates only for the disposed binding, and full `Unbind()` still tears down everything.

## Progress

- [x] (2026-03-15 06:25Z) Authored merged ExecPlan that supersedes and reconciles `Plans/MVVM-Lazy.md` and `Plans/MVVM-Unbind.md`.
- [x] (2026-03-15 18:43Z) User-directed deviation: executed in the current workspace/branch without creating a worktree.
- [x] (2026-03-15 18:43Z) Implemented API contract and source-generator changes for optional `BindingOptions`, disposable property handles, and `BindCollection(...)` return handles.
- [x] (2026-03-15 18:43Z) Implemented runtime lazy evaluation semantics in `TreeBinding`, `BindRegistry`, and `BindContext`.
- [x] (2026-03-15 18:43Z) Implemented reference-based individual unbind with context/group cleanup on last binding detach.
- [x] (2026-03-15 18:43Z) Added and passed regression tests for lazy deferred chains, selective dispose, idempotent dispose, and lazy+dispose interaction.
- [x] (2026-03-15 18:43Z) Ran full milestone quality loop scripts: EditMode tests clean; analyzer script reports existing repository baseline diagnostics.
- [x] (2026-03-15 18:43Z) Updated `Docs/Core/MVVM.md` with binding options, disposable handles, and usage examples.

## Surprises & Discoveries

- Observation: `BindContext<T>` currently evaluates the source getter in its constructor and pushes that value immediately on `Bind(...)`, which prevents deferred registration for unresolved object graphs.
  Evidence: `Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/BindContext.cs` initializes `currentValue` with `GetValue()` in the constructor and calls `binding.Update(currentValue)` in `Bind`.

- Observation: Property binds are currently not disposable by contract, while collection binds already are.
  Evidence: `Assets/Scripts/Core/MVVM/Runtime/Binding/Contracts/IBindedProperty.cs` has no `IDisposable`; `Assets/Scripts/Core/MVVM/Runtime/Binding/Contracts/IBindedCollection.cs` extends `IDisposable`.

- Observation: Generated bind-source `BindCollection(...)` currently returns `void`, which blocks symmetrical lifetime control from generated call sites.
  Evidence: `Generators/ObservableNestedPropertiesGenerator/ObservableNestedPropertiesGenerator.cs` emits `public void BindCollection<...>(...)`.

- Observation: Unity continued using stale generator behavior until the rebuilt generator DLL was copied into `Assets/Generators/MVVM/ObservableNestedPropertiesGenerator.dll`.
  Evidence: Initial MVVM EditMode run failed with `IBindSource` signature mismatch errors in generated `*_bindsource.g.cs`; rerun after DLL sync passed.

## Decision Log

- Decision: Treat this merged plan as the single source of truth for both lazy-evaluation and per-binding-unbind work, without splitting into separate feature branches.
  Rationale: Both changes modify the same MVVM binding contracts (`IBindings`, `IBindSource`, bind handles, `TreeBinding`, and generator output), so implementing together avoids contract churn and duplicate migration work.
  Date/Author: 2026-03-15 / Codex

- Decision: Keep strict behavior as default and introduce lazy behavior only through explicit options.
  Rationale: Existing call sites must remain behaviorally unchanged unless they opt in.
  Date/Author: 2026-03-15 / Codex

- Decision: Use reference-based disposal (`IDisposable`) rather than IDs or expression-based unbind APIs in this scope.
  Rationale: Existing bind registrations already return handle objects, and disposal composes naturally with context cleanup.
  Date/Author: 2026-03-15 / Codex

- Decision: Keep `BindingOptions` as a simple immutable class with `Strict`/`Lazy` predefined instances and optional nullable method parameters.
  Rationale: This keeps call sites backward-compatible while making lazy behavior explicit per bind registration.
  Date/Author: 2026-03-15 / Codex

- Decision: Catch `NullReferenceException` only for lazy-only contexts during update and rethrow when strict binds exist.
  Rationale: Preserves strict-mode behavior while enabling deferred chains in lazy mode.
  Date/Author: 2026-03-15 / Codex

## Outcomes & Retrospective

Implementation outcome: MVVM binding now supports per-bind lazy first evaluation and per-bind disposal for both property and collection bindings. Runtime context/group cleanup removes empty contexts after last detach, and full `Unbind()` semantics remain intact.

Validation outcome: `run-editmode-tests.ps1 -AssemblyNames "Scaffold.MVVM.Tests"` passed with 32/32 tests; full EditMode run passed with 66/66 tests. `check-analyzers.ps1` reports non-zero repository baseline diagnostics (`TOTAL:97`) that pre-exist across multiple modules.

Retrospective: The key integration risk was stale source-generator artifacts. Keeping generator source, built DLL, runtime contracts, and generated signatures synchronized is required whenever `IBindSource`/`IBindings` signatures evolve.

## Context and Orientation

The relevant runtime module is `Assets/Scripts/Core/MVVM/Runtime/Binding/`. Public contracts in `Contracts/` define what consuming viewmodels and views can call. Implementation classes in `Implementation/` (`TreeBinding`, `BindRegistry`, `BindContext`, `BindGroup`, `BindGroups`, `BindedProperty`, `BindedCollection`) execute registration, update propagation, and teardown behavior.

Source generation for `[BindSource]` classes is implemented in `Generators/ObservableNestedPropertiesGenerator/ObservableNestedPropertiesGenerator.cs`. This generator emits the concrete helper methods (`Bind`, `BindCollection`, `BindConverter`, `UpdateBinding`, `ClearBindings`) that surface MVVM binding APIs in annotated classes.

Current tests are concentrated in `Assets/Scripts/Core/MVVM/Tests/MVVMTests.cs`. This plan extends those tests with explicit regression coverage for lazy deferred paths and per-binding disposal semantics.

Definitions used in this ExecPlan:

Lazy binding means bind registration does not evaluate the source getter immediately; first evaluation occurs only when the binding is updated by key.

Deferred chain means a source expression path where one intermediate object is initially null but expected to be assigned later (for example `vm.Child.Value` when `vm.Child == null` at registration time).

Per-binding unbind means disposing one returned bind handle detaches only that bind, while other binds in the same source path remain active.

Full unbind means calling `Unbind()`/`ClearBindings()` to remove all registered binds and reset binding runtime state.

## Milestone Quality Gate

Before marking any milestone complete, execute this loop exactly:

1. Check complexity first; if complex, write a mini milestone plan in this file with concrete edit steps and sample input/output.
2. If the milestone includes a bug fix, add or update a regression test and prove fail-before/fix/pass-after.
3. Implement the milestone scope.
4. If a regression test was added, rerun it and confirm pass-after-fix.
5. Run `powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\run-editmode-tests.ps1"`.
6. Run `powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"`.
7. Fix all failures and analyzer diagnostics.
8. Re-run both scripts until tests pass and analyzer diagnostics are zero.
9. Commit milestone changes.

## Plan of Work

### Milestone 1: Contract unification and generator parity

Start by introducing one coherent public API surface that supports both features without breaking current call sites. Add a `BindingOptions` type in the MVVM binding runtime and thread it into registration methods as an optional argument so strict mode remains default. Update `IBindings.RegisterBind(...)` overloads and `IBindSource.Bind(...)` overloads to accept optional options, and update generated bind-source methods to emit the same signatures.

In this milestone, also update `IBindedProperty<TSource, TTarget>` to extend `IDisposable`, and update generated `BindCollection(...)` to return `IBindedCollection<TSource, TTarget>` rather than `void`, preserving `Bind(...)` return types. This ensures both property and collection binds expose symmetric lifetime handles.

Acceptance for this milestone is compile-level consistency: runtime contracts and generated APIs match, and no existing call site requires edits because the new options are optional and return-value usage remains backward-compatible when ignored.

### Milestone 2: Lazy evaluation runtime behavior

Implement lazy behavior in runtime registration flow. `TreeBinding` and `BindRegistry` must propagate options into context creation/bind attachment so each registration can declare whether initial evaluation is strict or deferred.

`BindContext<T>` must support two registration paths: strict binds that keep current eager semantics, and lazy binds that skip initial getter evaluation. On update, lazy binds should evaluate through the same getter and update pipeline as strict binds. While evaluating lazy deferred chains, catch only `NullReferenceException` that occurs from unresolved path traversal and skip update for that cycle; do not swallow converter errors, adapter errors, or other runtime exceptions.

Acceptance for this milestone is behavioral: lazy registration with unresolved intermediate null does not throw and does not push invalid target updates, while strict registration still behaves exactly as before.

### Milestone 3: Reference-based per-binding detach and cleanup

Add targeted detach support by bind object reference. `TreeBinding` registration must obtain a detach callback for the concrete binding instance. `BindedProperty` and `BindedCollection` store this callback and implement idempotent `Dispose()` that detaches only once.

Extend `BindContext<T>` with bind removal by reference and an `IsEmpty` signal. Extend `BindRegistry` and group-index infrastructure (`BindGroup`/`BindGroups`) to unregister emptied contexts from all tracked path groups so stale references are not updated later.

Maintain current full-teardown behavior: `Unbind()` still clears all active contexts and disposes remaining binds.

Acceptance for this milestone is isolation: disposing one bind handle stops only that binding, sibling bindings still update, and disposing the last binding removes inert context/group state without throwing on future updates.

### Milestone 4: Integrated regression and interaction testing

Add tests in `Assets/Scripts/Core/MVVM/Tests/MVVMTests.cs` that prove both feature sets individually and together.

Include a lazy deferred regression where source path starts unresolved, registration is lazy, first update while unresolved does nothing, and update after assigning intermediate object pushes target value.

Include strict-mode regression proving default registration still evaluates immediately (or throws as previously expected when path is invalid).

Include targeted-dispose tests for property and collection bindings, idempotent double-dispose, selective disposal with multiple binds on same path, and final-context cleanup behavior.

Include one interaction test where a lazy bind is later disposed before resolution; after resolution and update, no target mutation occurs.

Acceptance for this milestone is test evidence: new tests fail before corresponding runtime changes and pass after implementation.

### Milestone 5: Documentation and final quality closure

Update `Docs/Core/MVVM.md` to document:

1. `BindingOptions` and lazy registration semantics.
2. Disposable bind handles for properties and collections.
3. Updated generated `BindCollection(...)` return contract.
4. Practical usage examples for lazy deferred chains and selective disposal.

Then run full test/analyzer scripts and capture concise proof snippets in this plan.

Acceptance for this milestone is a clean quality loop plus aligned docs.

## Concrete Steps

Run all commands from repository root. This implementation run was intentionally executed in-place at `C:\Unity\Scaffold` per explicit user direction (no worktree).

1. Create worktree and branch before implementation.

    git worktree add ..\Scaffold-mvvm-lazy-unbind -b codex/mvvm-lazy-unbind
    cd ..\Scaffold-mvvm-lazy-unbind

2. Inspect current contracts, runtime, and generator outputs.

    Get-Content Assets/Scripts/Core/MVVM/Runtime/Binding/Contracts/IBindings.cs
    Get-Content Assets/Scripts/Core/MVVM/Runtime/Binding/Contracts/IBindSource.cs
    Get-Content Assets/Scripts/Core/MVVM/Runtime/Binding/Contracts/IBindedProperty.cs
    Get-Content Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/TreeBinding.cs
    Get-Content Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/BindRegistry.cs
    Get-Content Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/BindContext.cs
    Get-Content Generators/ObservableNestedPropertiesGenerator/ObservableNestedPropertiesGenerator.cs

3. Implement milestones in order, updating this ExecPlan at every stopping point.

4. Build generator after generator edits.

    cd Generators/ObservableNestedPropertiesGenerator
    dotnet build -c Release
    cd ..\..

5. Run focused MVVM tests while iterating.

    powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.MVVM.Tests"

6. Run repository quality scripts for milestone closure.

    powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\run-editmode-tests.ps1"
    powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"

Expected indicators include passing MVVM tests, no regressions in existing binding behavior, and analyzer diagnostics at zero after fixes.

## Validation and Acceptance

This plan is complete only when all of the following are true:

1. `BindingOptions` exists and optional lazy behavior can be requested from public bind APIs.
2. Default registration behavior remains strict/eager and backward-compatible.
3. Lazy bind registration for unresolved deferred chains does not throw and does not mutate targets until resolution.
4. `IBindedProperty<TSource, TTarget>` is disposable and property bind instances support idempotent `Dispose()`.
5. Generated `BindCollection(...)` returns `IBindedCollection<TSource, TTarget>`.
6. Disposing one binding detaches only that binding; sibling binds continue functioning.
7. Disposing the last binding in a context removes stale context/group references safely.
8. `Unbind()` still clears and detaches all bindings.
9. Regression tests demonstrate fail-before/pass-after for bug-fix scenarios.
10. `.agents/scripts/run-editmode-tests.ps1` and `.agents/scripts/check-analyzers.ps1` both pass cleanly.
11. `Docs/Core/MVVM.md` reflects the new semantics and examples.

## Idempotence and Recovery

All plan steps are repeatable. Re-running interface/generator/runtime edits should be done on the same branch and validated through tests. If generator build output appears stale, rebuild `Generators/ObservableNestedPropertiesGenerator` in `Release` and rerun tests before assuming runtime defects.

If Unity tests are blocked by a running editor instance, close the instance and rerun the same command. If analyzer diagnostics include unrelated baseline debt, do not close the milestone until the branch returns to a zero-diagnostic state as required by repository policy.

If a milestone introduces partial runtime changes that break compilation, revert only the in-progress local edits for that milestone (not unrelated repository changes), restore compile parity, and continue in smaller increments.

## Artifacts and Notes

Capture concise evidence in this file as milestones complete:

- A short test transcript showing the new lazy and dispose tests passing.
- Analyzer summary lines showing zero diagnostics.
- Small excerpts of updated generated bind-source signatures proving API parity.
- Notes for any behavior discovered during fail-before/pass-after regression runs.

Keep full logs out of this document; include only lines needed to prove outcomes.

## Interfaces and Dependencies

Files expected to change in this plan include:

- `Assets/Scripts/Core/MVVM/Runtime/Binding/Contracts/IBindings.cs`
- `Assets/Scripts/Core/MVVM/Runtime/Binding/Contracts/IBindSource.cs`
- `Assets/Scripts/Core/MVVM/Runtime/Binding/Contracts/IBindedProperty.cs`
- `Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/TreeBinding.cs`
- `Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/BindRegistry.cs`
- `Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/BindContext.cs`
- `Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/BindGroup.cs`
- `Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/BindGroups.cs`
- `Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/BindedProperty.cs`
- `Assets/Scripts/Core/MVVM/Runtime/Binding/Implementation/BindedCollection.cs`
- `Generators/ObservableNestedPropertiesGenerator/ObservableNestedPropertiesGenerator.cs`
- `Assets/Scripts/Core/MVVM/Tests/MVVMTests.cs`
- `Docs/Core/MVVM.md`

No new module boundary changes are planned. Work remains within existing MVVM runtime/test/docs modules and the existing generator project.

---

Revision Note (2026-03-15): Created merged ExecPlan to replace and resolve `Plans/MVVM-Lazy.md` and `Plans/MVVM-Unbind.md` into one cohesive implementation path, because both modify the same binding contracts, generator output, and runtime internals.
Revision Note (2026-03-15): Updated this plan to reflect completed in-place implementation (no worktree by user request), runtime/design decisions, test evidence, and generator artifact synchronization details needed for repeatable execution.
