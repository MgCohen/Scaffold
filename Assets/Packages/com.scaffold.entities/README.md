# com.scaffold.entities

# Entities (Core)

## TL;DR

- Purpose: **Definition** / **instance** flyweight model with **`AttributeSO`** identity and **`AttributeValueType`**, typed **`AttributeValue`** payloads (`FloatAttributeValue`, `IntAttributeValue`, `BoolAttributeValue`, `StringAttributeValue`), **`Attribute`** record keys, **`IAttributeBag` / `AttributeBag`** (parent-chained base resolution, structural add/remove events), instance-only **modifiers**, **`InstanceId`** (runtime int id), and **factories**. Instances store **only** divergent state: **`modifiedValueCache`** while modifiers are active for a key, and **`instanceBag`** for runtime-only attribute slots. **`IEntity<TDefinition>`** exposes **`GetValue<T>`**, **`GetAttribute<TAttr>`**, and **`TryGetAttribute<TAttr>`**, plus **`Subscribe` / `Subscribe<T>` / `SubscribeToAttribute<TAttr>`** (each returns **`IDisposable`** for unsubscription; **`Unsubscribe`** remains for named **`Action<AttributeValue>`** delegates), **`AddRuntimeAttribute` / `RemoveRuntimeAttribute`**, and **`SubscribeToAttributeAdded` / `SubscribeToAttributeRemoved`**. Optional **behavior runner** (`EntityBehaviorRunner<TData,TInput>`) for per-frame arbitration.
- Location: `Assets/Packages/com.scaffold.entities/Runtime/` — **`Core/`** (definitions, attributes, instances, factory) and **`Behavior/`** (behavior contracts and runner). Single assembly `Scaffold.Entities`; runtime tests in `Tests/Runtime/` (`Scaffold.Entities.Tests`), editor tests in `Tests/Editor/` (`Scaffold.Entities.Editor.Tests`).
- **Unity coupling:** References `UnityEngine` (`MonoBehaviour`, `ScriptableObject`). `Scaffold.Entities.asmdef` has `noEngineReferences: false`. See [Architecture.md](../../../Architecture.md): the Core folder does not mean “no Unity.”
- Depends on: Unity engine only (no cross-assembly references to other first-party modules in this repository snapshot).
- **Consumers:** Add a reference from `Scaffold.Entities` in your module’s `.asmdef` when you use these types in gameplay or presentation code.

## Folder layout (conceptual split)

| Folder | Responsibility |
|--------|----------------|
| `Runtime/Core/` | `Attribute`, `AttributeSO`, `AttributeValue` hierarchy, `AttributeEntry`, `IAttributeBag`, `AttributeBag`, `EntityDefinition`, `EntityInstance<TDefinition>`, `EntityModifierEntry`, `EntityBehaviour` / `EntityBehaviour<TDefinition>`, `IEntity<TDefinition>`, `EntityInstanceFactory`, `InstanceId` — stats, modifiers, parent-chained attribute bags, instance storage. |
| `Runtime/Behavior/` | `IEntityBehavior`, `IEntityFrameInputProvider`, `EntityBehaviorRunner` — per-frame behavior arbitration. |
| `Samples/Example/` | Optional **Scaffold.Entities.Samples** assembly (`autoReferenced: false`): scripts, `SampleEntity` prefab under `Assets/Prefabs/`, authored attributes + `SampleCharacterDefinition` under `Assets/Data/Authoring/` — definition → instance, numeric modifier combine, and `EntityBehaviorRunner` with WASD movement. |

## Samples

- Assembly: `Samples/Example/Scaffold.Entities.Samples.asmdef` — reference it from your game assembly if you want to open or extend the sample types.
- Assets: `Samples/Example/Assets/Data/Authoring/` — health, move speed, stunned attributes and `SampleCharacterDefinition` (defaults wired as typed `FloatAttributeValue` rows).
- Prefab: `Samples/Example/Assets/Prefabs/SampleEntity` — drop into a scene and press Play: console logs show base stats plus a `+25` health modifier (float sum); on-screen HUD shows effective values; **WASD / arrow keys** move on the XZ plane using the effective Move Speed.

Package layout index: `Documentation/entities.md`. Changelog: `CHANGELOG.md`.

## Public API (selection)

| Symbol | Role |
|--------|------|
| `Attribute` | Record key: **`Key`** string + **`AttributeValueType`** (structural equality for dictionary keys). |
| `AttributeSO` | `ScriptableObject` slot identity with **`ValueType`** dropdown; **implicit** conversion to `Attribute` (asset **name** + type). |
| `AttributeValue` | Abstract base; concrete **`FloatAttributeValue`** / **`IntAttributeValue`** / **`BoolAttributeValue`** / **`StringAttributeValue`** (SerializeReference on definitions and modifiers). |
| `AttributeEntry` | One definition row: **`AttributeSO`** + **`BaseValue`** (`[SerializeReference]`). |
| `IAttributeBag` | Read contract: **`Parent`**, **`TryGetBase`**, **`LocalKeys`**. |
| `AttributeBag` | Serialized **`Entries`** list + runtime lookup; optional **`SetParent`** for chained reads (child wins); **`Add` / `Remove`** (local only, structural events); **`RebuildCache`** syncs from serialized rows. |
| `EntityDefinition` | Shared defaults: **`Entries`** (via embedded **`AttributeBag`**); runtime **`TryGetBaseValue`**. Modifiers are **not** on definitions. |
| `EntityInstance<TDefinition>` | Serializable state: `InstanceId`, `TDefinition`, **`instanceBag`** (runtime slots; starts empty), modifiers; **`GetValue<T>`**, **`GetAttribute<TAttr>`**, **`TryGetAttribute<TAttr>`**; **`Subscribe`**, **`Subscribe<T>`** (raw value), **`SubscribeToAttribute<TAttr>`** (typed **`AttributeValue`**); **`AddRuntimeAttribute` / `RemoveRuntimeAttribute`**; structural subscriptions; modifier list mutation. Reads resolve **`modifiedValueCache`** first, then **`instanceBag.TryGetBase`** (walks to the definition bag). |
| `EntityInstanceFactory` | **`CreateInstance<TDefinition>`** and **`CreateOnGameObject<TEntity,TDefinition>`**. |
| `EntityBehaviour` | Abstract `MonoBehaviour` base for runners and heterogeneous lists. |
| `EntityBehaviour<TDefinition>` | Host with **`EntityInstance<TDefinition>`**; implements **`IEntity<TDefinition>`** by delegation. |
| `IEntity<out TDefinition>` | **`Id`**, **`Definition`**, typed getters for attributes; attribute change subscriptions (**`IDisposable`** token or **`Unsubscribe`**); runtime attribute slots and structural add/remove subscriptions. |
| `InstanceId` | **`record InstanceId(int Id)`** — monotone runtime id per process (`New()`). |
| `EntityModifierEntry` | Instance-only modifier: **`AttributeSO`** + **`ModifierValue`**, or **`Attribute`** + **`ModifierValue`** (runtime). |
| `IEntityBehavior<TData,TInput>` | Behavior contract: `TryAcceptControl`, `Execute`, `OnQuit`. |
| `IEntityFrameInputProvider<TInput>` | `GetFrameInput()` for runners that need per-frame context. |
| `EntityBehaviorRunner<TData,TInput>` | Runs behaviors in order; first accepting behavior wins; tracks `OnQuit` when switching flows. |

## Lookup semantics

- **Resolve by slot:** use **`Attribute`** keys at runtime (pass an **`AttributeSO`** where needed; implicit conversion supplies **`Attribute`**). **`EntityModifierEntry`** can be built from **`AttributeSO`** (serialized) or **`Attribute`** (runtime-only).
- **Effective values:** **`AttributeModifierHandler`** applies instance modifiers on demand: each **`AttributeValue`** subtype implements **`Combine`** (**float/int**: sum then clamp to base min/max; **bool**: last modifier wins; **string**: concatenate **`StringAttributeValue`** contributions after the base).

## Testing

- Assemblies: `Scaffold.Entities.Tests` (EditMode, `Tests/Runtime/`), `Scaffold.Entities.Editor.Tests` (`Tests/Editor/`). Run via `.agents/scripts/run-editmode-tests.ps1` or full `validate-changes.ps1`.
- `EntityInstanceTests` and **`AttributeBagTests`** (`Tests/Runtime/`) cover definition resolution, flyweight caching, float modifier sum, **`ClearModifiers`** restoring base values, invalid removal, runtime attribute slots, structural events, `CreateOnGameObject`, and positive **`InstanceId.Id`**. **`EntityComponentEditor`** shows the definition **`AttributeBag`** read-only in the inspector; **`AttributeBagPropertyDrawer`** edits serialized entry lists.

## Related

- `../../../Docs/App/AppStartup.md` (composition; wire consumers when you add gameplay modules that reference `Scaffold.Entities`).
- `../../../Architecture.md` (module boundaries).
- ExecPlan: `Plans/EntitiesExpand/EntitiesExpand-ExecPlan.md`, `Plans/EntitiesCleanup/EntitiesCleanup-ExecPlan.md`.
