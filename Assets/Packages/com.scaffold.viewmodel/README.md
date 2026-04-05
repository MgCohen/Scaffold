# com.scaffold.viewmodel

# Scaffold.MVVM.ViewModel

## TL;DR

- Purpose: MVVM orchestration layer for bind registration, lifecycle reset, and navigation-aware context.
- Location: `Assets/Packages/com.scaffold.viewmodel/`.
- Depends on: `Scaffold.MVVM`, `Scaffold.Navigation` (plus CommunityToolkit.Mvvm precompiled refs for `ObservableObject` on `ViewModel`).
- Used by: `Scaffold.MVVM.View` and app presentation controllers.
- Runtime/Editor: runtime + EditMode tests.

## Responsibilities

- Owns `IViewModel` and the `ViewModel` base class (`Runtime/Contracts/IViewModel.cs` and `ViewModel.cs`).
- Owns navigation-aware bind lifecycle (`Bind(INavigation)`), nested property registration, and stale-binding cleanup via the shared bind source.
- Consumes binding infrastructure from `Scaffold.MVVM` (`TreeBinding`, `BindingOptions`, bind contracts).
- Does not own Unity `MonoBehaviour` view concerns or the binding engine implementation.

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `IViewModel` | MVVM controller contract | navigation/context bind calls | standardized controller lifecycle | invalid consumer assumptions can break runtime behavior |
| `ViewModel` | Base implementation with binding orchestration | `Bind(INavigation)` + binding registrations | initialized state + binding graph | stale registrations are cleared on rebind |

For `TreeBinding`, `BindingOptions`, `IBindedProperty<>`, and related bind APIs, see `../com.scaffold.mvvm/README.md`.

## Setup / Integration

1. Add references to `Scaffold.MVVM.ViewModel` and `Scaffold.Navigation`.
2. Inherit screen controllers from `ViewModel`.
3. Call `Bind(INavigation)` at controller startup/bind entry.
4. Register bindings in `Initialize()` or equivalent bind lifecycle methods.

Common setup mistakes:
- Skipping `Bind(INavigation)` lifecycle entry.
- Holding stale handles across rebinds.

Fast checks:
- Rebinding should clear old registrations before new initialization.
- Module tests should pass with no binding lifecycle regressions.

## How to Use

1. Define state in `Model` descendants and expose through `ViewModel`.
2. Register binds with MVVM bind APIs.
3. Use `BindingOptions.Lazy` only when deferred/null-path-safe behavior is required.
4. Dispose individual binding handles only when partial teardown is needed.

## Examples

### Minimal

```csharp
public partial class InventoryViewModel : ViewModel
{
    [ObservableProperty] private InventoryModel model = new InventoryModel();
    [ObservableProperty] private int value;

    protected override void Initialize()
    {
        Bind(() => model.Value, () => Value);
    }
}
```

### Realistic

```mermaid
sequenceDiagram
  participant Nav as INavigation
  participant VM as ViewModel
  participant Bind as TreeBinding

  Nav->>VM: Bind(navigation)
  VM->>VM: Clear stale registrations
  VM->>Bind: Register Bind(...)
  VM-->>Bind: Property changed
  Bind-->>VM: Target update applied
```

### Guard / Error path

```csharp
// Rebind-safe pattern: rely on framework cleanup at Bind(navigation).
public void OnNavigationContextChanged(INavigation navigation)
{
    Bind(navigation);
}
```

## Best Practices

- Treat `Bind(INavigation)` as the lifecycle reset point.
- Keep binding expressions explicit and stable.
- Use lazy bindings intentionally, not by default.
- Keep domain logic in models/services, not in glue binding code.
- Prefer framework bind APIs over manual property notification wiring.

## Anti-Patterns

- Manual `PropertyChanged` subscribe/unsubscribe in MVVM descendants.
- Calling `OnPropertyChanged(...)` manually where bind APIs should be used.
- Keeping disposed/invalid binding handles and reusing them after rebind.

## Testing

- Test assembly: `Scaffold.MVVM.ViewModel.Tests`.
- Run:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.MVVM.ViewModel.Tests"
```

- Expected: all tests pass, zero failures.
- Bugfix rule: add/update regression test first, verify fail-before/fix/pass-after.

## AI Agent Context

- Invariants:
  - `Bind(INavigation)` clears stale bindings before re-initialization.
  - per-binding disposal affects only that registration.
  - strict vs lazy binding behavior remains explicit and test-covered.
- Allowed Dependencies:
  - `Scaffold.MVVM`, `Scaffold.Navigation`.
- Forbidden Dependencies:
  - Unity view lifecycle classes (`MonoBehaviour` view concerns).
- Change Checklist:
  - verify `Scaffold.MVVM.ViewModel.Tests` passes.
  - verify rebind behavior still clears stale registrations.
  - verify lazy/strict behavior and handle disposal paths.
- Known Tricky Areas:
  - deferred access chains with `BindingOptions.Lazy`.
  - nested model replacement across active bindings.

## Related

- `../../../Architecture.md`
- `../com.scaffold.model/README.md`
- `../com.scaffold.view/README.md`
- `../com.scaffold.navigation/README.md`

## Changelog

- Binding implementation moved to `Scaffold.MVVM`; this module keeps `ViewModel` / `IViewModel` and navigation lifecycle only.
- Added split module file.
- Reorganized from legacy combined MVVM doc and expanded to module standard.

- Added coverage for close-without-navigation behavior and repeated bind initialization flow.
- Consolidated `Scaffold.MVVM.ViewModel.Contracts` into `Scaffold.MVVM.ViewModel` and moved boundary types to `Runtime/Contracts/`.
