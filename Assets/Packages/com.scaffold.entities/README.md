# com.scaffold.entities

# Entities (Core)

## TL;DR

- Purpose: **Definition** / **instance** flyweight model with **`AttributeSO`** identity and **`AttributeValueType`**, typed **`AttributeValue`** payloads (`FloatAttributeValue`, `IntAttributeValue`, `BoolAttributeValue`, `StringAttributeValue`), **`Attribute`** record keys, instance-only **modifiers**, **`InstanceId`** (runtime int id), and **factories**. **`IEntity<TDefinition>`** exposes **`GetValue<T>`**, **`GetAttribute<TAttr>`**, and **`TryGetAttribute<TAttr>`**. Optional **behavior runner** (`EntityBehaviorRunner<TData,TInput>`) for per-frame arbitration.
- Location: `Assets/Packages/com.scaffold.entities/Runtime/` — **`Core/`** (definitions, attributes, instances, factory) and **`Behavior/`** (behavior contracts and runner). Single assembly `Scaffold.Entities`; tests in `Assets/Packages/com.scaffold.entities/Tests/` (`Scaffold.Entities.Tests`).
- **Unity coupling:** References `UnityEngine` (`MonoBehaviour`, `ScriptableObject`). `Scaffold.Entities.asmdef` has `noEngineReferences: false`. See [Architecture.md](../../../Architecture.md): the Core folder does not mean “no Unity.”
- Depends on: Unity engine only (no cross-assembly references to other first-party modules in this repository snapshot).
- **Consumers:** Add a reference from `Scaffold.Entities` in your module’s `.asmdef` when you use these types in gameplay or presentation code.

## Folder layout (conceptual split)

| Folder | Responsibility |
|--------|----------------|
| `Runtime/Core/` | `Attribute`, `AttributeSO`, `AttributeValue` hierarchy, `AttributeEntry`, `EntityDefinition`, `EntityInstance<TDefinition>`, `EntityModifierEntry`, `EntityBehaviour` / `EntityBehaviour<TDefinition>`, `IEntity<TDefinition>`, `EntityInstanceFactory`, `InstanceId` — stats, modifiers, instance storage. |
| `Runtime/Behavior/` | `IEntityBehavior`, `IEntityFrameInputProvider`, `EntityBehaviorRunner` — per-frame behavior arbitration. |
| `Samples/` | Optional **Scaffold.Entities.Samples** assembly (`autoReferenced: false`): `SampleEntity` prefab, authored attributes + `SampleCharacterDefinition`, `SampleCharacterEntity`, and scripts showing definition → instance, numeric modifier combine, and `EntityBehaviorRunner` with WASD movement. |

## Samples

- Assembly: `Samples/Scaffold.Entities.Samples.asmdef` — reference it from your game assembly if you want to open or extend the sample types.
- Assets: `Samples/Authoring/` — `Attribute_Health`, `Attribute_MoveSpeed`, and `SampleCharacterDefinition` (defaults wired to those slots as typed `FloatAttributeValue` rows).
- Prefab: `Samples/SampleEntity` — drop into a scene and press Play: console logs show base stats plus a `+25` health modifier (float sum); on-screen HUD shows effective values; **WASD / arrow keys** move on the XZ plane using the effective Move Speed.

## Public API (selection)

| Symbol | Role |
|--------|------|
| `Attribute` | Record key: **`Key`** string + **`AttributeValueType`** (structural equality for dictionary keys). |
| `AttributeSO` | `ScriptableObject` slot identity with **`ValueType`** dropdown; **implicit** conversion to `Attribute` (asset **name** + type). |
| `AttributeValue` | Abstract base; concrete **`FloatAttributeValue`** / **`IntAttributeValue`** / **`BoolAttributeValue`** / **`StringAttributeValue`** (SerializeReference on definitions and modifiers). |
| `AttributeEntry` | One definition row: **`AttributeSO`** + **`BaseValue`** (`[SerializeReference]`). |
| `EntityDefinition` | Shared defaults: **`Entries`** list; runtime **`TryGetBaseValue`**. Modifiers are **not** on definitions. |
| `EntityInstance<TDefinition>` | Serializable state: `InstanceId`, `TDefinition`, modifiers; **`GetValue<T>`**, **`GetAttribute<TAttr>`**, **`TryGetAttribute<TAttr>`**, modifier list mutation. |
| `EntityInstanceFactory` | **`CreateInstance<TDefinition>`** and **`CreateOnGameObject<TEntity,TDefinition>`**. |
| `EntityBehaviour` | Abstract `MonoBehaviour` base for runners and heterogeneous lists. |
| `EntityBehaviour<TDefinition>` | Host with **`EntityInstance<TDefinition>`**; implements **`IEntity<TDefinition>`** by delegation. |
| `IEntity<out TDefinition>` | **`Id`**, **`Definition`**, typed getters for attributes. |
| `InstanceId` | **`record InstanceId(int Id)`** — monotone runtime id per process (`New()`). |
| `EntityModifierEntry` | Instance-only modifier: **`AttributeSO`** + **`ModifierValue`**, or **`Attribute`** + **`ModifierValue`** (runtime). |
| `IEntityBehavior<TData,TInput>` | Behavior contract: `TryAcceptControl`, `Execute`, `OnQuit`. |
| `IEntityFrameInputProvider<TInput>` | `GetFrameInput()` for runners that need per-frame context. |
| `EntityBehaviorRunner<TData,TInput>` | Runs behaviors in order; first accepting behavior wins; tracks `OnQuit` when switching flows. |

## Lookup semantics

- **Resolve by slot:** use **`Attribute`** keys at runtime (pass an **`AttributeSO`** where needed; implicit conversion supplies **`Attribute`**). **`EntityModifierEntry`** can be built from **`AttributeSO`** (serialized) or **`Attribute`** (runtime-only).
- **Effective values:** **`AttributeModifierHandler`** applies instance modifiers on demand: each **`AttributeValue`** subtype implements **`Combine`** (**float/int**: sum then clamp to base min/max; **bool**: last modifier wins; **string**: concatenate **`StringAttributeValue`** contributions after the base).

## Testing

- Assembly: `Scaffold.Entities.Tests` (EditMode). Run via `.agents/scripts/run-editmode-tests.ps1` or full `validate-changes.ps1`.
- `EntityInstanceTests` covers definition resolution, float modifier sum, **`RemoveModifierAt` / `ClearModifiers`** restoring base values, invalid removal, `CreateOnGameObject`, and positive **`InstanceId.Id`**.

## Related

- `../../../Docs/App/AppStartup.md` (composition; wire consumers when you add gameplay modules that reference `Scaffold.Entities`).
- `../../../Architecture.md` (module boundaries).
- ExecPlan: `Plans/EntitiesExpand/EntitiesExpand-ExecPlan.md`, `Plans/EntitiesCleanup/EntitiesCleanup-ExecPlan.md`.
