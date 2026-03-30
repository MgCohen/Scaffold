# Scaffold.MVVM

## TL;DR

- Purpose: shared MVVM contract primitives reused by model, viewmodel, and view modules.
- Location: `Assets/Scripts/Infra/MVVM/Runtime/`.
- Depends on: `Scaffold.Maps` (multi-key maps for bind registry); CommunityToolkit.Mvvm and System.Runtime.CompilerServices.Unsafe as explicit precompiled references, loaded from `Assets/Generators/MVVM/` (shared with the MVVM source generators).
- Used by: `Scaffold.MVVM.Model`, `Scaffold.MVVM.ViewModel`, `Scaffold.MVVM.View`.
- Runtime/Editor: runtime contract assembly only.

## Responsibilities

- Owns shared MVVM contract primitives (`INestedObservableProperties`, nested-observable attributes, adapter/converter abstractions).
- Owns binding infrastructure: `BindingOptions`, `TreeBinding`, bind registry/context/set types, and binding contracts (`IBindedProperty<>`, `IBindings`, etc.).
- Owns expression helpers used for bind registration (`ExpressionsUtility`).
- Does not own navigation-aware view-model lifecycle (`ViewModel.Bind(INavigation)`) or Unity `MonoBehaviour` view types; those stay in `Scaffold.MVVM.ViewModel` / `Scaffold.MVVM.View`.
- Boundaries: shared MVVM mechanics only; no app-specific screens or domain models.

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `INestedObservableProperties` | Contract for registering nested observable members. | Registration requests from implementing type. | Exposes nested observable registration contract. | Misuse is compile-time/implementation concern, not runtime exception by contract. |
| `NestedObservableObjectAttribute` | Marks types that participate in nested observable processing. | Attribute placement on class/type. | Metadata consumed by analyzers/runtime integrations. | Invalid usage surfaces via analyzer or consumer validation. |
| `NestedPropertyAttribute` | Marks nested properties for observable traversal. | Attribute placement on property/member. | Metadata used by binding/observable consumers. | Invalid placement is surfaced by analyzer/consumer checks. |
| `Adapter<T>` | Defines target adaptation contract for bindings. | Source value/context from binder. | Adapted target value/type. | Adapter implementation decides guard behavior. |
| `Converter<TFrom, TTo>` | Defines value conversion contract. | `TFrom` source value. | `TTo` converted value. | Converter implementation decides guard behavior. |
| `BindingOptions` | Shared options for strict/lazy binding behavior. | Option flags/values. | Consistent options payload for bind registration. | Invalid combinations are handled by bind consumers. |
| `TreeBinding` / `IBindings` | Bind registration and update graph. | Source/target expressions, handlers, converters. | Propagates updates on property/collection changes. | Strict mode can throw on bad paths; lazy mode swallows some null-path cases. |
| `IBindedProperty<,>` / `IBindedCollection<,>` | Disposable binding handles. | Return values from `RegisterBind` APIs. | Per-handle teardown. | Disposing detaches only that registration. |

## Setup / Integration

1. Add asmdef reference to `Scaffold.MVVM` from modules that need MVVM base contracts.
2. Consume contract types from `Scaffold.MVVM.Binding` namespaces instead of re-defining equivalents.
3. Keep navigation and screen lifecycle in `ViewModel` / `View`; keep bind mechanics here.
4. Fast check: `Scaffold.MVVM` should not require references to Unity-specific assemblies.

## How to Use

1. Implement `INestedObservableProperties` in model-like types that expose nested observable members.
2. Annotate relevant types/members with nested-observable attributes where registration metadata is needed.
3. Use `Adapter<>` and `Converter<,>` contracts for binding translation points in dependent modules.
4. Pass shared `BindingOptions` through higher-level bind setup to keep behavior consistent.

## Examples

### Minimal

```csharp
public sealed class PlayerStats : INestedObservableProperties
{
    // Module consumers implement registration semantics.
}
```

### Realistic

```csharp
public sealed class HealthToTextConverter : Converter<int, string>
{
    public override string Convert(int value) => $"HP: {value}";
}
```

### Guard / Error path

```csharp
public sealed class StrictPositiveConverter : Converter<int, int>
{
    public override int Convert(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value;
    }
}
```

## Best Practices

- Keep this module free of runtime orchestration and Unity object concerns.
- Prefer extending existing base contracts over adding parallel contract types.
- Treat attributes here as boundary metadata and validate usage in analyzers or consumer modules.
- Keep contracts stable to avoid ripple changes across ViewModel/View modules.
- Use analyzer checks after contract changes to catch boundary regressions quickly.

## Anti-Patterns

- Adding navigation or view-layer lifecycle into binding types.
  Migration: keep `INavigation` / `MonoBehaviour` usage in `Scaffold.MVVM.ViewModel` and `Scaffold.MVVM.View`.
- Duplicating binding types in ViewModel or View assemblies.
  Migration: extend `Scaffold.MVVM` instead.
- Duplicating adapter/converter abstractions in dependent modules.
  Migration: reuse `Adapter<>` and `Converter<,>` from this module.

## Testing

- Test assemblies: covered by dependent module tests (`Scaffold.MVVM.Model.Tests`, `Scaffold.MVVM.ViewModel.Tests`, `Scaffold.MVVM.View.Tests`) and analyzer tests.
- Run:

```powershell
dotnet test "Analyzers/Scaffold/Scaffold.Analyzers.Tests/Scaffold.Analyzers.Tests.csproj" -c Release --nologo
dotnet test "Generators/Scaffold.Mvvm.Analyzers.Tests/Scaffold.Mvvm.Analyzers.Tests.csproj" -c Release --nologo
& ".\.agents\scripts\validate-changes.cmd"
```

- Expected pass signal: analyzer tests pass and the validation gate reports no failures/diagnostics.
- Bugfix rule: add/update a regression test in the affected consuming module before applying the fix.

## AI Agent Context

- Invariants:
  - Binding engine and shared bind contracts live in this assembly.
  - Types stay reusable by Model, ViewModel, and View paths.
- Allowed Dependencies:
  - BCL, `Scaffold.Maps`, CommunityToolkit.Mvvm (precompiled), minimal UnityEngine usage only where bind infrastructure logs or integrates with Unity diagnostics (same as prior ViewModel binding code).
- Forbidden Dependencies:
  - `Scaffold.Navigation` and other app/flow modules (keep navigation on `ViewModel`).
  - Domain/gameplay assemblies.
- Change Checklist:
  - Update this doc for any API/contract changes.
  - Run analyzer tests and validation gate.
  - Verify dependent modules still compile/reference unchanged symbols.
- Known Tricky Areas:
  - Attribute semantics can drift from analyzer expectations.
  - Generic adapter/converter signature changes cascade broadly.

## Related

- `Docs/Infra/Model.md`
- `Docs/Core/ViewModel.md`
- `Docs/App/View.md`
- `Architecture.md`
- `Docs/Testing.md`

## Changelog

- Folder and assembly renamed from `BaseMVVM` / `Scaffold.MVVM.Base` to `MVVM` / `Scaffold.MVVM`; test assembly `Scaffold.MVVM.Tests`.
- Documented that runtime binding implementation (`TreeBinding`, registry, contracts) lives in this module alongside base contracts.
- Reworked to module documentation standard sections, added usage/examples/anti-pattern/testing and AI context details.
- Initial module baseline authored for MVVM base contracts.
