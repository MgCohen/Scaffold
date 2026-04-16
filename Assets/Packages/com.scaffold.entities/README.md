# com.scaffold.entities

# Entities (Core)

## TL;DR

- Purpose: **Definition** / **instance** model with **`VariableSO`** identity and **`VariableValueType`**, typed **`VariableValue`** payloads (`FloatVariableValue`, `IntVariableValue`, `BoolVariableValue`, `StringVariableValue`), **`Variable`** record keys, **`IVariableBag` / `VariableBag`** (parent-chained base resolution), instance-only **modifiers**, **`InstanceId`** (runtime int id), and **instance-scoped creation** via **`EntityInstanceCreator<TDefinition>`** with an injectable **`IInstanceIdGenerator`**. Each instance uses a **three-bag chain**: definition **`VariableBag`** (shared bases) → **`instanceBaseBag`** (runtime-only bases; structural **`OnVariableAdded` / `OnVariableRemoved`**) → **`instanceEffectiveBag`** (modifier results via silent local writes). Reads use **`TryGetBase`** on the effective bag.
- **Interfaces:** **`IReadOnlyEntity<TDefinition>`** (read + subscribe base overload + structural add/remove subscriptions), **`IEntity<TDefinition>`** (adds **`AddVariable` / `RemoveVariable`**), **`IInstance<TDefinition>`** (adds modifier APIs). **`EntityInstance<TDefinition>`** and **`EntityComponent<TDefinition>`** implement **`IInstance`**. Internal **`EntityExtensions`** provides typed **`Subscribe`**, **`SubscribeToVariable`**, **`AddVariable<T>`**, and **`AddModifier<T>`** overloads (same assembly; visible to tests via **`InternalsVisibleTo`**).
- **Location:** `Assets/Packages/com.scaffold.entities/Runtime/` — **`Core/`** and **`Behavior/`**. Assemblies: `Scaffold.Entities`, `Scaffold.Entities.Tests`, `Scaffold.Entities.Editor`, `Scaffold.Entities.Editor.Tests`, optional **`Scaffold.Entities.Samples`**.
- **Unity coupling:** References `UnityEngine`. See [Architecture.md](../../../Architecture.md).
- **Consumers:** Reference `Scaffold.Entities` from your module `.asmdef`.

## Folder layout (conceptual split)

| Folder | Responsibility |
|--------|----------------|
| `Runtime/Core/` | `Variable`, `VariableSO`, `VariableValue` hierarchy, `VariableEntry`, `IVariableBag`, `VariableBag`, `EntityDefinition`, `EntityInstance<TDefinition>`, `EntityModifierEntry`, `EntityModifierEntryAsset`, `EntityComponent` / `EntityComponent<TDefinition>`, `IReadOnlyEntity`, `IEntity`, `IInstance`, `EntityExtensions`, `VariableValueFactory`, creators, ids. |
| `Runtime/Behavior/` | `IEntityBehavior`, `IEntityFrameInputProvider`, `EntityBehaviorRunner`. |
| `Samples/Scripts/` | Optional samples assembly. |

## Samples

- Assembly: `Samples/Scripts/Scaffold.Entities.Samples.asmdef`.
- Assets: `Samples/Assets/Data/Authoring/` — variable assets, `SampleCharacterDefinition`, and `SampleHealthBonusModifier` (`EntityModifierEntryAsset`).
- Prefab: `Samples/Assets/Prefabs/SampleEntity`.

Authoritative module pointer: [`Docs/Core/Entities.md`](../../../Docs/Core/Entities.md).

## Public API (selection)

| Symbol | Role |
|--------|------|
| `Variable` | Record key: **`Key`** + **`VariableValueType`**. |
| `VariableSO` | `ScriptableObject` slot; implicit conversion to **`Variable`**. |
| `VariableValue` | Abstract base; concrete float/int/bool/string value types. |
| `VariableEntry` | Serializable definition row (used by **`VariableBag`** and Unity drawers); authoring fields are not part of the public read surface. |
| `IVariableBag` | Read: **`Parent`**, **`TryGetBase`**, **`LocalKeys`**. |
| `VariableBag` | Chained reads, **`Add` / `Remove`**, silent local writes for effective layer. |
| `EntityDefinition` | **`TryGetBaseValue`**, **`AddVariable(VariableSO, VariableValue)`**; serialized variable rows are internal to the package. |
| `EntityInstance<TDefinition>` | Instance state; **`IInstance`**; **`NotifyAllEffectiveValues`** (editor / play-mode debug; internal to the runtime assembly). |
| `IReadOnlyEntity<out TDefinition>` | **`GetValue`**, **`TryGetValue`**, **`GetVariable`**, **`TryGetVariable`**, **`Subscribe(Variable, Action<VariableValue>)`**, **`Unsubscribe`**, structural subscriptions. |
| `IEntity<out TDefinition>` | **`AddVariable` / `RemoveVariable`**. |
| `IInstance<TDefinition>` | **`AddModifier` / `RemoveModifier` / `ClearModifiers`**. |
| `EntityModifierEntry` | **`VariableSO`** or runtime **`Variable`** + **`ModifierValue`**; **`Key`** property. |
| `EntityModifierEntryAsset` | `ScriptableObject` wrapping one **`EntityModifierEntry`** for editor authoring; explicit cast to **`EntityModifierEntry`** (C# allows only one user-defined conversion between these types). |
| `EntityComponent<TDefinition>` | Host; **`OnValidate`** (play mode) rebroadcasts effective bag edits to subscribers. |

**Runtime read boundary:** use **`GetValue` / `TryGetValue`**, **`GetVariable` / `TryGetVariable`**, and **`Subscribe`** on **`IReadOnlyEntity`** (or **`EntityInstance` / `EntityComponent`**). Do not rely on walking into serialized definition rows; **`Definition`**, **`Instance`**, and bag **`Entries`** are internal to **`Scaffold.Entities`**.

## Lookup semantics

- **Resolve by slot:** use **`Variable`** keys (implicit from **`VariableSO`** where convenient).
- **Three-bag chain:** definition → **`instanceBaseBag`** → **`instanceEffectiveBag`**.
- **Structural vs value:** base bag **`Add` / `Remove`** fire **`OnVariableAdded` / `OnVariableRemoved`**; modifier recalculation uses silent effective writes and **`VariableNotifier`**.
- **Combine:** **`VariableModifierHandler`**; numeric sum + clamp; bool last wins; string concat.

## Testing

- `EntityInstanceTests`, **`VariableBagTests`** under `Tests/Runtime/`.
- Editor drawer registration: **`VariablePropertyDrawerEditorTests`**, **`EntityModifierEntryAssetEditorTests`** (modifier drawer + wrapper cast).

## Related

- `../../../Docs/App/AppStartup.md`, `../../../Architecture.md`.
- ExecPlan references under `Plans/`.
