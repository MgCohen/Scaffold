# Refactor All Assets/Scripts Modules: Add Tests, Samples, and Fix Analyzer Errors

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` (at the repository root).

The final artifact of this plan is the ExecPlan file itself, written to `Plans/Refactor-Modules/ExecPlan.md` in the repository, plus all implementation changes.


## Purpose / Big Picture

Every module in `Assets/Scripts/` must meet the project's three mandatory standards: automated tests, sample usage files, and zero Roslyn analyzer errors. Baseline was TOTAL:80. After this work, `check-analyzers.sh` will output TOTAL:0 (in-scope) and the Unity Test Runner will show all module tests passing.

The canonical reference is `Assets/Scripts/Tools/Maps/` — all Tests and Samples must mirror its structure.


## Progress

- [x] Create ExecPlan file at `Plans/Refactor-Modules/ExecPlan.md`
- [x] Run baseline `check-analyzers.sh` — TOTAL:80
- [x] Milestone 1: Maps — fix analyzer errors (tests and samples already existed)
- [x] Milestone 2: Records — add Tests, add Samples, fix analyzer errors
- [x] Milestone 3: Types — add Tests, add Samples, fix analyzer errors
- [x] Milestone 4: Containers — add Tests, add Samples, fix analyzer errors
- [x] Milestone 5: Events — add Tests, add Samples, fix analyzer errors
- [x] Milestone 6: NetworkMessages — add Tests, fix analyzer errors (Samples already existed)
- [x] Milestone 7: Navigation — add Tests, add Samples, fix analyzer errors
- [x] Milestone 8: MVVM — add Tests, add Samples, fix analyzer errors
- [x] Run final `check-analyzers.sh` — TOTAL:10, all 10 are out-of-scope AutoPacker. TOTAL:0 in-scope.
- [ ] Run Unity Test Runner — confirm all tests pass


## Surprises & Discoveries

- **SCA0006 counting rule**: Lines counted are BETWEEN the outer `{` and `}` of the method body (exclusive of those brace lines). Blank lines and inner brace lines all count. ≤8 is compliant; 9+ is a violation.
- **SCA0002 and delegates**: Only direct method calls trigger SCA0002 ordering. Method group assignments (`event += HandlerMethod`) do NOT trigger SCA0002 even if `HandlerMethod` appears before the event subscription line. This matters for `BindedCollection.HandleCollectionChanges`.
- **NavigationTransitions.cs was the hardest single file**: 14 violations required a complete restructure — 37 methods in total, topologically sorted by call graph. Key technique: introduce `RunAnimationIfPresent` as an intermediary to break a SCA0002 chain where `DoCloseSequence`/`DoHideSequence`/`DoOpenSequence` all called `GetAnimationSchema` which appeared before them.
- **ScriptableObject in tests**: Cannot use `new ScriptableObject()`. Must use `ScriptableObject.CreateInstance<T>()`. Applies to `ViewConfig` (a ScriptableObject subclass) in Navigation tests.
- **INavigation interface must be fully implemented in stubs**: `INavigation` has a `NavigationPoint CurrentPoint { get; }` property that was initially omitted from the stub in `NavigationUseCases.cs`, causing a compile error. Always implement all interface members in stub inner classes.
- **AutoPacker violations are out of scope**: `Assets/Generators/Autopacker/` has 10 violations (2 files). These are pre-existing and not part of this plan.
- **Current analyzer state** after Milestones 1–7: TOTAL:30 (20 in-scope MVVM + 10 out-of-scope AutoPacker).


## Decision Log

- Decision: Process modules in order of increasing complexity (Maps → Records → Types → Containers → Events → NetworkMessages → Navigation → MVVM).
  Rationale: Simpler modules establish patterns. MVVM is last as the most complex.
  Date/Author: 2026-03-08 / planning

- Decision: Use `Assets/Scripts/Tools/Maps/` as the canonical reference for test and sample structure.
  Rationale: Maps has approved `Tests/` and `Samples/` with proper `.asmdef` files.
  Date/Author: 2026-03-08 / planning

- Decision: Records module gets minimal tests (one struct with `init` setter to prove shim works).
  Rationale: `IsExternalInit.cs` is a compiler shim with no logic to test.
  Date/Author: 2026-03-08 / planning

- Decision: Navigation and MVVM tests are Editor-mode using `optionalUnityReferences: ["TestAssemblies"]`. Pure C# logic only; no scene hierarchy.
  Rationale: Unity Test Runner EditMode runs without a Player. BindedProperty, EventLedger, NavigationStack etc. are pure C#.
  Date/Author: 2026-03-08 / planning

- Decision: For deep SCA0002 call chains, introduce intermediary helper methods ordered before all callers.
  Rationale: Topological sort of the call graph determines valid file ordering. Intermediaries can break otherwise-circular ordering requirements.
  Date/Author: 2026-03-08 / implementation


## Outcomes & Retrospective

(To be populated at completion.)


## Milestone 8: MVVM — Remaining Work

**Module path**: `Assets/Scripts/Core/MVVM/`
**asmdef name**: `Scaffold.MVVM` (at `Assets/Scripts/Core/MVVM/Runtime/Scaffold.MVVM.asmdef`)

### Files with violations (TOTAL:20)

| File | Count | Violations |
|------|-------|-----------|
| `Runtime/Binding/Implementation/BindedCollection.cs` | 5 | 4× SCA0006, 1× SCA0002 |
| `Runtime/Implementation/ViewElement.cs` | 3 | SCA0006 |
| `Runtime/Implementation/EventLedger.cs` | 3 | SCA0006 |
| `Runtime/Binding/Implementation/BindedProperty.cs` | 2 | SCA0006 |
| `Runtime/Binding/Implementation/BindSet.cs` | 2 | SCA0006 |
| `Runtime/Implementation/ViewEvents.cs` | 1 | SCA0002 |
| `Runtime/Implementation/View.cs` | 1 | SCA0002 |
| `Runtime/Implementation/UIView.cs` | 1 | SCA0006 (`SetCanvas` 14 lines) |
| `Runtime/Binding/Implementation/ExpressionsUtility.cs` | 1 | SCA0006 (`GetPropertyName` 15 lines) |
| `Runtime/Binding/Implementation/BindingPath.cs` | 1 | SCA0006 (`Create` 13 lines) |

### Tests to create

**File**: `Assets/Scripts/Core/MVVM/Tests/Scaffold.MVVM.Tests.asmdef`

```json
{
    "name": "Scaffold.MVVM.Tests",
    "rootNamespace": "Scaffold.MVVM.Tests",
    "references": ["Scaffold.MVVM"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false,
    "optionalUnityReferences": ["TestAssemblies"]
}
```

**File**: `Assets/Scripts/Core/MVVM/Tests/MVVMTests.cs`

Focus: `BindedProperty<T>` change notification, `BindedCollection<T>` add/remove, `EventLedger` dispatch. All are pure C# — no Unity types needed.

### Samples to create

**File**: `Assets/Scripts/Core/MVVM/Samples/Scaffold.MVVM.Samples.asmdef`

```json
{
    "name": "Scaffold.MVVM.Samples",
    "rootNamespace": "Scaffold.MVVM.Samples",
    "references": ["Scaffold.MVVM"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**File**: `Assets/Scripts/Core/MVVM/Samples/MVVMUseCases.cs`

Demonstrate: ViewModel with `BindedProperty`, binding to callback, mutating and observing.


## Reference: asmdef Patterns

Tests `.asmdef`:
```json
{
    "name": "Scaffold.[Module].Tests",
    "rootNamespace": "Scaffold.[Module].Tests",
    "references": ["Scaffold.[Module]"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false,
    "optionalUnityReferences": ["TestAssemblies"]
}
```

Samples `.asmdef`:
```json
{
    "name": "Scaffold.[Module].Samples",
    "rootNamespace": "Scaffold.[Module].Samples",
    "references": ["Scaffold.[Module]"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```


## Validation and Acceptance

Complete when:

1. `bash "C:/Users/user/Documents/Unity/Scaffold/.agents/scripts/check-analyzers.sh"` outputs TOTAL:20 or less (AutoPacker 10 violations are pre-existing out-of-scope). In-scope target: TOTAL:0 for `Assets/Scripts/` files.
2. Unity Test Runner (EditMode) shows passing tests for all 8 assemblies: `Scaffold.Maps.Tests`, `Scaffold.Records.Tests`, `Scaffold.Types.Tests`, `Scaffold.Containers.Tests`, `Scaffold.Events.Tests`, `Scaffold.NetworkMessages.Tests`, `Scaffold.Navigation.Tests`, `Scaffold.MVVM.Tests`.
3. Each module has a `Samples/` folder with public `UseCase*()` methods.

Recovery: Running `check-analyzers.sh` is always safe (read-only). If a `BLOCKER:` line appears after a fix, revert and try a different approach. Never suppress diagnostics with attributes or `.editorconfig`.


