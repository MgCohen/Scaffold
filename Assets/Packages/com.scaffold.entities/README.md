# com.scaffold.entities

# Entities (Core)

## TL;DR

- Purpose: Float-backed attributes (`EntityAttribute` + `EntityAttributeEntry`), additive modifiers, and an ordered behavior runner (`EntityBehaviorRunner<TData,TInput>`) with per-frame input contracts.
- Location: `Assets/Packages/com.scaffold.entities/Runtime/` (`Scaffold.Entities`), tests in `Assets/Packages/com.scaffold.entities/Tests/` (`Scaffold.Entities.Tests`) when present.
- **Unity coupling:** References `UnityEngine` (`MonoBehaviour`, `ScriptableObject`). `Scaffold.Entities.asmdef` has `noEngineReferences: false`. See [Architecture.md](../../../Architecture.md): the Core folder does not mean “no Unity.”
- Depends on: Unity engine only (no cross-assembly references to other first-party modules in this repository snapshot).
- **Consumers:** Add a reference from `Scaffold.Entities` in your module’s `.asmdef` when you use these types in gameplay or presentation code.

## Public API

| Symbol | Role |
|--------|------|
| `EntityAttribute` | `ScriptableObject` id for a stat/flag; logical id is the asset `name`. |
| `EntityAttributeEntry` | Serialized entry: attribute reference, base float, effective value (base + modifiers); optional `OnValueChanged` when effective value changes. |
| `EntityAttributeModifierEntry` | Serializable pair: attribute reference + additive delta applied in `Entity` recalculation. |
| `Entity` | `MonoBehaviour`: `attributeEntries`, `attributeModifiers`, modifier add/remove/clear, `Get/SetFloat/BoolAttribute`, `AttributeValueChanged`. |
| `IEntityBehavior<TData,TInput>` | Behavior contract: `TryAcceptControl`, `Execute`, `OnQuit`. |
| `IEntityFrameInputProvider<TInput>` | `GetFrameInput()` for runners that need per-frame context. |
| `EntityBehaviorRunner<TData,TInput>` | Runs behaviors in order; first accepting behavior wins; tracks `OnQuit` when switching flows. |

## Testing

- Assembly: `Scaffold.Entities.Tests` (EditMode) when test sources are added under `Assets/Packages/com.scaffold.entities/Tests/`.

## Related

- `../../../Docs/App/AppStartup.md` (composition; wire consumers when you add gameplay modules that reference `Scaffold.Entities`).
- `../../../Architecture.md` (module boundaries).
