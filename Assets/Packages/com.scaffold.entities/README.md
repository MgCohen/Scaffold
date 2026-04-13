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
| `Runtime/Core/` | `Variable`, `VariableSO`, `VariableValue` hierarchy, `VariableEntry`, `IVariableBag`, `VariableBag`, `EntityDefinition`, `EntityInstance<TDefinition>`, `EntityModifierEntry`, `EntityComponent` / `EntityComponent<TDefinition>`, `IReadOnlyEntity`, `IEntity`, `IInstance`, `EntityExtensions`, `VariableValueFactory`, creators, ids. |
| `Runtime/Behavior/` | `IEntityBehavior`, `IEntityFrameInputProvider`, `EntityBehaviorRunner`. |
| `Samples/Scripts/` | Optional samples assembly. |

## Samples

- Assembly: `Samples/Scripts/Scaffold.Entities.Samples.asmdef`.
- Assets: `Samples/Assets/Data/Authoring/` — variable assets and `SampleCharacterDefinition`.
- Prefab: `Samples/Assets/Prefabs/SampleEntity`.

Authoritative module pointer: [`Docs/Core/Entities.md`](../../../Docs/Core/Entities.md).

## Public API (selection)

| Symbol | Role |
|--------|------|
| `Variable` | Record key: **`Key`** + **`VariableValueType`**. |
| `VariableSO` | `ScriptableObject` slot; implicit conversion to **`Variable`**. |
| `VariableValue` | Abstract base; concrete float/int/bool/string value types. |
| `VariableEntry` | Definition row: **`VariableSO`** + **`BaseValue`**. |
| `IVariableBag` | Read: **`Parent`**, **`TryGetBase`**, **`LocalKeys`**. |
| `VariableBag` | **`Entries`**, chained reads, **`Add` / `Remove`**, silent local writes for effective layer. |
| `EntityDefinition` | **`Entries`**, **`TryGetBaseValue`**, **`AddVariable(VariableSO, VariableValue)`**. |
| `EntityInstance<TDefinition>` | Instance state; **`IInstance`**; **`NotifyAllEffectiveValues`** (editor / play-mode debug). |
| `IReadOnlyEntity<out TDefinition>` | **`GetValue`**, **`GetVariable`**, **`TryGetVariable`**, **`Subscribe(Variable, Action<VariableValue>)`**, **`Unsubscribe`**, structural subscriptions. |
| `IEntity<out TDefinition>` | **`AddVariable` / `RemoveVariable`**. |
| `IInstance<TDefinition>` | **`AddModifier` / `RemoveModifier` / `ClearModifiers`**. |
| `EntityModifierEntry` | **`VariableSO`** or runtime **`Variable`** + **`ModifierValue`**; **`Key`** property. |
| `EntityComponent<TDefinition>` | Host; **`OnValidate`** (play mode) rebroadcasts effective bag edits to subscribers. |

## Lookup semantics

- **Resolve by slot:** use **`Variable`** keys (implicit from **`VariableSO`** where convenient).
- **Three-bag chain:** definition → **`instanceBaseBag`** → **`instanceEffectiveBag`**.
- **Structural vs value:** base bag **`Add` / `Remove`** fire **`OnVariableAdded` / `OnVariableRemoved`**; modifier recalculation uses silent effective writes and **`VariableNotifier`**.
- **Combine:** **`VariableModifierHandler`**; numeric sum + clamp; bool last wins; string concat.

## Testing

- `EntityInstanceTests`, **`VariableBagTests`** under `Tests/Runtime/`.
- Editor drawer registration: **`VariablePropertyDrawerEditorTests`**.

## Related

- `../../../Docs/App/AppStartup.md`, `../../../Architecture.md`.
- ExecPlan references under `Plans/`.
