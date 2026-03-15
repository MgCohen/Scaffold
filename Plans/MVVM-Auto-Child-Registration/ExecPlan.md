# Normalize Nested Child Registration for Replaced ObservableProperty Instances

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this change, view models should no longer need manual calls such as `RegisterChildProperty(...)` when an `[ObservableProperty]` reference is replaced. Nested binding paths should continue to update automatically after instance replacement, purely through generated plumbing.

Success is observable when a core MVVM regression test reproducing nested reference replacement passes without any manual child-registration hook in the test fixture view model.

## Progress

- [x] (2026-03-15 21:08Z) Cleared branch working-tree changes to start this plan from a clean baseline.
- [x] (2026-03-15 21:08Z) Authored this ExecPlan for generator-level normalization.
- [x] (2026-03-15 21:18Z) Added MVVM regression tests for nested reference replacement rewire and stale-instance detachment behavior.
- [x] (2026-03-15 21:18Z) Captured fail-before evidence: `Scaffold.MVVM.Tests.MVVMTests.ViewModel_ReplacingNestedObservableProperty_RewiresNestedUpdates` failed before generator changes.
- [x] (2026-03-15 21:22Z) Implemented generator-driven nested rewire via generated per-property `On{Property}Changed(...)` hooks and detachable child subscription state.
- [x] (2026-03-15 21:24Z) Rebuilt generator and synced updated DLL to `Assets/Generators/MVVM/ObservableNestedPropertiesGenerator.dll`.
- [x] (2026-03-15 21:25Z) Verified pass-after for focused MVVM suite (`Scaffold.MVVM.Tests`: 34/34 passed).
- [x] (2026-03-15 21:26Z) Updated `Docs/Core/MVVM.md` with automatic nested replacement behavior.
- [ ] (2026-03-15 21:27Z) Analyzer script remains non-zero due repository baseline diagnostics outside this feature scope (`TOTAL:97`).

## Surprises & Discoveries

- Observation: Nested property updates currently require explicit child registration when a nested object reference is reassigned in view-model-only scenarios.
  Evidence: Existing MVVM tests require explicit `RegisterNestedProperties()` setup and do not yet cover automatic rewire on nested reference replacement.

- Observation: `NestedPropertyAttribute` exists but is currently unused in runtime modules for this exact replacement scenario.
  Evidence: Search results show no active runtime usage path besides attribute definition and generator scanning.

- Observation: The test runner script can throw while formatting failed test names, even though results XML is produced correctly.
  Evidence: `run-editmode-tests.ps1` failed with parameter transformation error on one-failure runs; parsing the generated XML showed exact failing case.

## Decision Log

- Decision: Normalize behavior in the source generator instead of adding conventions per feature module.
  Rationale: This removes repetitive, error-prone manual hooks and keeps behavior consistent across all MVVM classes.
  Date/Author: 2026-03-15 / Codex

- Decision: Keep API surface backward-compatible and avoid requiring caller-side migration where possible.
  Rationale: Existing modules should gain the behavior automatically after generator update.
  Date/Author: 2026-03-15 / Codex

## Outcomes & Retrospective

Implemented outcome: generator output now tracks nested child subscriptions per tracked member and rewires on `[ObservableProperty]` replacement through generated partial hooks. Replaced instances are detached and new instances are attached automatically.

Validation outcome:

- Focused MVVM tests: `34 total / 34 passed / 0 failed`.
- Full EditMode tests: `68 total / 68 passed / 0 failed`.
- Analyzer check: non-zero repository baseline remains (`TOTAL:97`) in unrelated modules.

Target behavior is now present in core MVVM fixtures without any module-specific manual child-registration method.

## Context and Orientation

Relevant code areas:

- `Generators/ObservableNestedPropertiesGenerator/ObservableNestedPropertiesGenerator.cs`
  This generator emits `*_nested.g.cs` and `*_bindsource.g.cs` files for classes marked with MVVM attributes.
- `Assets/Scripts/Core/MVVM/Runtime/Implementation/ViewModel.cs`
  View model base type with `[NestedObservableObject]` and binding integration.
- `Assets/Scripts/Core/MVVM/Runtime/Binding/Contracts/INestedObservableProperties.cs`
  Contract for nested registration behavior.
- `Assets/Scripts/Core/MVVM/Tests/MVVMTests.cs`
  Repro scenario for replaced nested object instances will be implemented here via dedicated test fixtures.

Definition used in this plan:

Auto child registration means generated code automatically manages nested `PropertyChanged` wiring for newly assigned nested objects and unhooks old objects, so nested bind paths continue working after replacement.

## Milestone Quality Gate

Before marking any milestone complete, execute this loop:

1. Check complexity first; if complex, add a mini milestone plan with concrete steps and acceptance notes.
2. If bug-fix behavior is covered, add/update regression tests and prove fail-before/fix/pass-after.
3. Implement the milestone.
4. Re-run the regression test and confirm pass.
5. Run `powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\run-editmode-tests.ps1"`.
6. Run `powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"`.
7. Fix all test failures and analyzer diagnostics.
8. Re-run both scripts until clean.
9. Commit milestone changes.

## Plan of Work

### Milestone 1: Reproduce and lock behavior with regression tests

Add regression tests in `Assets/Scripts/Core/MVVM/Tests/MVVMTests.cs` using minimal test fixtures (for example `ReplacementHostModel` and `ReplacementNestedModel`) that assert:

- nested bind path updates when the initial nested instance changes values,
- replacing the nested instance rewires updates to the new instance,
- old instance changes do not continue to mutate the bound target.

Keep a failing case (before generator fix) that demonstrates why manual child registration had to be injected.

### Milestone 2: Generator-level auto-registration for nested reference replacements

Update `ObservableNestedPropertiesGenerator` so it emits replacement-safe child wiring for tracked nested members. For each generated tracked property that can host nested observable state, generated code should:

- detach previous child subscription when value changes,
- attach new child subscription when value is assigned,
- route nested property path updates through existing `OnPropertyChanged("Parent.Child")` behavior.

Prefer generating explicit per-property backing fields/delegates for subscription bookkeeping instead of anonymous lambdas that cannot be detached.

### Milestone 3: Remove manual hooks from feature code

If any temporary/manual nested-registration hooks were introduced in MVVM test fixtures to prove fail-before behavior, remove them and keep only generator-driven behavior. Ensure regression coverage still passes.

### Milestone 4: Verify cross-module safety

Run the full EditMode suite and analyzer checks. If generator changes affect other MVVM classes, add targeted tests in `Assets/Scripts/Core/MVVM/Tests/MVVMTests.cs` for generic replacement behavior on simple test fixtures.

### Milestone 5: Documentation update

Update `Docs/Core/MVVM.md` with:

- how nested reference replacement now works automatically,
- any constraints (supported field/property patterns),
- examples showing no manual registration method is needed.

## Concrete Steps

Run from repository root `C:\Unity\Scaffold`.

1. Inspect generator and current nested registration flow.

    Get-Content Generators/ObservableNestedPropertiesGenerator/ObservableNestedPropertiesGenerator.cs
    rg -n "RegisterChildProperty|On.*Changed|NestedProperty" Assets/Scripts Generators -g "*.cs"

2. Add/adjust regression tests in MVVM tests.

    Get-Content Assets/Scripts/Core/MVVM/Tests/MVVMTests.cs

3. Implement generator updates and rebuild generator project.

    cd Generators/ObservableNestedPropertiesGenerator
    dotnet build -c Release
    cd ..\..

4. Run quality loop scripts.

    powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\run-editmode-tests.ps1"
    powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"

5. Update docs and re-run quality loop.

## Validation and Acceptance

Acceptance criteria:

1. MVVM replacement-flow tests pass using only generator-driven nested wiring.
2. Replacing a nested `[ObservableProperty]` instance rewires updates to the new instance.
3. Old replaced instances no longer emit nested updates to the parent object.
4. Full EditMode tests pass.
5. Analyzer script reports zero diagnostics.
6. `Docs/Core/MVVM.md` documents the automatic behavior and usage guidance.

## Idempotence and Recovery

Generator edits are safe to iterate. If generated behavior appears stale:

- rebuild generator (`dotnet build -c Release`),
- ensure the updated DLL is the one used by Unity/IDE,
- re-run tests.

If replacement behavior regresses broadly, revert only the generator milestone commit and keep test additions to preserve bug repro coverage.

## Artifacts and Notes

Store concise evidence in this plan as implementation progresses:

- failing test snippet before generator change,
- passing test snippet after change,
- final summary lines from test and analyzer scripts,
- short generated-code excerpt proving detach/attach on replacement.

## Interfaces and Dependencies

Primary files expected to change:

- `Generators/ObservableNestedPropertiesGenerator/ObservableNestedPropertiesGenerator.cs`
- `Assets/Scripts/Core/MVVM/Tests/MVVMTests.cs`
- `Docs/Core/MVVM.md`

No new modules are required; this work stays within existing MVVM generator/runtime/test/documentation boundaries.

---

Revision Note (2026-03-15): Created this ExecPlan after cleaning the branch, to normalize nested replacement behavior through generator automation instead of manual per-viewmodel hooks.
Revision Note (2026-03-15): Updated progress, discoveries, and outcomes after implementing generator-based nested replacement rewiring, adding MVVM regression tests, and running validation scripts.
