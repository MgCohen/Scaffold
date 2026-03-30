# Scaffold.MVVM.Model

## TL;DR

- Purpose: Unity-free observable model base for MVVM state.
- Location: `Assets/Scripts/Infra/Model/Runtime/`.
- Depends on: `Scaffold.MVVM.Model` runtime depends on `Scaffold.MVVM`.
- Used by: `Scaffold.MVVM.ViewModel`, `Scaffold.MVVM.View`, and app/runtime modules that expose observable model state.
- Runtime/Editor: runtime + EditMode tests.

## Responsibilities

- Owns `Model` base type for observable data.
- Owns nested-observable metadata contracts/attributes.
- Does not own navigation, binding orchestration, or Unity `MonoBehaviour` view lifecycle.
- Boundaries: pure C# runtime module (`noEngineReferences=true`).

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `Model` | Base observable model state type | model properties (often source-generated) | property notifications | n/a |
| `NestedObservableObjectAttribute` | Marks types for nested observable wiring | target type/usage on class | metadata consumed by MVVM tooling | n/a |
| `NestedPropertyAttribute` | Marks nested properties for wiring | target property | metadata consumed by MVVM tooling | n/a |
| `INestedObservableProperties` | Nested-observable contract | implementer member set | consistent nested propagation contract | missing implementation breaks nested updates |

## Setup / Integration

1. Add assembly reference to `Scaffold.MVVM.Model` for runtime model base types.
2. Add assembly reference to `Scaffold.MVVM` when consuming `Scaffold.MVVM.Binding` contract types directly.
3. Inherit model types from `Model`.
4. Keep the module Unity-free (no `UnityEngine` references).
5. Validate setup with model tests.

Common setup mistake:
- Adding Unity-facing concerns to model types. Fast check: keep code compatible with `noEngineReferences=true`.

## How to Use

1. Define observable state in `Model` descendants.
2. Use model instances from `ViewModel` descendants for screen state orchestration.
3. Use nested observable attributes/contracts for nested state propagation where needed.

## Examples

### Minimal

```csharp
public partial class PlayerModel : Model
{
    [ObservableProperty] private int health;
}
```

### Realistic

```csharp
public partial class InventoryModel : Model, INestedObservableProperties
{
    [ObservableProperty] private ItemModel selectedItem;
    [ObservableProperty] private int totalValue;
}
```

### Guard / Error path

```csharp
// Anti-pattern: this module must remain Unity-free.
// using UnityEngine; // do not add in Scaffold.MVVM.Model
```

## Best Practices

- Keep model classes focused on data, not orchestration.
- Keep state shape explicit and small.
- Use nested observable metadata only where nested propagation is required.
- Keep module APIs stable because many modules consume model contracts.
- Preserve pure C# constraints to prevent Unity coupling leaks.

## Anti-Patterns

- Putting navigation or screen lifecycle behavior in `Model`.
- Referencing `UnityEngine` in model runtime code.
- Combining multiple unrelated domain concerns in a single model class.

## Testing

- Test assembly: `Scaffold.MVVM.Model.Tests`.
- Run:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.MVVM.Model.Tests"
```

- Expected: all tests pass, zero failures.
- Bugfix rule: add/update regression test first, verify fail-before/fix/pass-after.

## AI Agent Context

- Invariants:
  - module remains Unity-free (`noEngineReferences=true`).
  - `Model` remains the base observable data abstraction for MVVM.
- Allowed Dependencies:
  - `Scaffold.MVVM.Model` runtime: `Scaffold.MVVM`.
- Forbidden Dependencies:
  - `UnityEngine`, app modules, and navigation/view modules.
- Change Checklist:
  - verify `Scaffold.MVVM.Model.Tests` passes.
  - verify no new runtime assembly references were added.
  - verify nested observable behavior is still covered.
- Known Tricky Areas:
  - nested observable replacement/propagation assumptions across consumers.

## Related

- `Architecture.md`
- `Docs/Core/ViewModel.md`
- `Docs/App/View.md`
- `Docs/AutomatedTesting.md`

## Changelog

- Added split module file.
- Reorganized from legacy combined MVVM doc and expanded to module standard.

- Added contract-attribute coverage for `NestedPropertyAttribute` and `NestedObservableObjectAttribute` usage constraints.
