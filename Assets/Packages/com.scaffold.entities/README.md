# com.scaffold.entities

# Entities (Core)

## TL;DR

- Purpose: **Definition** / **instance** model with **`VariableSO`** identity and stable **`payloadTypeId`** strings (via **`[VariableValueId]`** on concrete **`VariableValue`** types), typed payloads (`FloatVariableValue`, `IntVariableValue`, `BoolVariableValue`, `StringVariableValue`), **`Variable`** keys (**`Key`** + **`PayloadTypeId`**), **`IVariableBag` / `VariableBag`** (parent-chained base resolution), instance-only **modifiers**, **`InstanceId`** (runtime int id), and **instance-scoped creation** via **`EntityInstanceCreator<TDefinition>`** with an injectable **`IInstanceIdGenerator`**. Each instance uses a **three-bag chain**: definition **`VariableBag`** (shared bases) → **`instanceBaseBag`** (runtime-only bases; structural **`OnVariableStructuralChange`**) → **`instanceEffectiveBag`** (modifier results via silent local writes). Reads use **`TryGetBase`** on the effective bag.
- **Interfaces:** **`IReadOnlyEntity<TDefinition>`** (read + subscribe base overload + **`SubscribeToVariableStructuralChanges`**) and **`IMutableEntity<TDefinition>`** (adds **`AddVariable` / `RemoveVariable`**, **`AddModifier`** returning **`ModifierId`**, **`RemoveModifier(Variable, ModifierId)`**, **`ClearModifiers`**). **`EntityInstance<TDefinition>`** and **`EntityComponent<TDefinition>`** implement **`IMutableEntity<TDefinition>`**. Internal **`EntityExtensions`** provides typed **`Subscribe`**, **`SubscribeToVariable`**, **`AddVariable<T>`**, **`AddModifier<T>`** ( **`Variable`** / **`VariableSO`** key), and **`AddModifier(..., string name, string payloadTypeId, T value)`** that builds **`new Variable(name, payloadTypeId)`** (caller supplies the correct payload id; no name-only resolution).
- **Location:** `Assets/Packages/com.scaffold.entities/Runtime/` — **`Core/`** and **`Behavior/`**. Assemblies: `Scaffold.Entities`, `Scaffold.Entities.Tests`, `Scaffold.Entities.Editor`, `Scaffold.Entities.Editor.Tests`, optional **`Scaffold.Entities.Samples`**.
- **Unity coupling:** References `UnityEngine`. See [Architecture.md](../../../Architecture.md).
- **Consumers:** Reference `Scaffold.Entities` from your module `.asmdef`.

## Folder layout (`Runtime/Core/`)

| Subfolder | Responsibility |
|-----------|----------------|
| **`Definitions/`** | `EntityDefinition`, `EntityDefinitionAsset`, `IEntityDefinition`, `IDefinitionVariableBagProvider`, `EntityModifierEntry`, `EntityModifierEntryAsset`. |
| **`Variables/`** | Keys and payloads: `Variable`, `VariableSO`, `VariableEntry`, `VariableValue` + **`Values/`**, `VariableValueIdAttribute`, `IVariableValue`. |
| **`VariableBags/`** | `IVariableBag`, `VariableBag`, `VariableStructuralChange` (parent chain for bases; structural notifications). |
| **`Instance/`** | Runtime entity slice: `BaseEntityInstance`, `EntityInstance` (+ editor partial), `EntityInstanceCreator`, `LocalVariableStorage`, `VariableModifierHandler`, `EntityVariableComputer`, `IEntityVariableStorage`. |
| **`Contracts/`** | `IReadOnlyEntity`, `IMutableEntity`. |
| **`Identity/`** | `InstanceId`, id generators (`IInstanceIdGenerator`, `IncrementingInstanceIdGenerator`), `ModifierId`. |
| **`Subscriptions/`** | `VariableNotifier`, `CallbackDisposable`, `EmptyDisposable`. |
| **`Hosting/`** | `EntityComponent`, `EntityComponent<TDefinition>`. |
| **`Utilities/`** | `EntityExtensions`, `VariableValueFactory`, **`VariableValueRegistry`** (internal id ↔ type map). |

## Folder layout (other runtime)

| Folder | Responsibility |
|--------|----------------|
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
| `Variable` | Key: **`Key`** + **`PayloadTypeId`** (stable string id, e.g. `float`, `string`). |
| `VariableSO` | `ScriptableObject` slot carrying **`PayloadTypeId`**; inspector uses a **payload type** popup; implicit conversion to **`Variable`**. |
| `VariableValue` | Abstract base; concrete subclasses declare **`[VariableValueId("…")]`**; factories and serialization resolve types via the internal registry (no silent fallback). |
| `VariableEntry` | Serializable definition row (used by **`VariableBag`** and Unity drawers); authoring fields are not part of the public read surface. |
| `IVariableBag` | Read: **`Parent`**, **`TryGetBase`**, **`LocalKeys`**. |
| `VariableBag` | Chained reads, **`Add` / `Remove`**, silent local writes for effective layer. |
| `EntityDefinition` | **`TryGetDefaultValue`**, **`AddVariable(Variable, VariableValue)`**; serialized variable rows are internal to the package. |
| `EntityInstance<TDefinition>` | Standalone mutable instance; inherits **`BaseEntityInstance<TDefinition>`**, implements **`IMutableEntity`**; **`NotifyAllEffectiveValues`** (editor / play-mode debug; internal to the runtime assembly). |
| `IReadOnlyEntity<out TDefinition>` | **`GetValue`**, **`TryGetValue`**, **`GetVariable`**, **`TryGetVariable`**, **`Subscribe(Variable, Action<VariableValue>)`**, **`Unsubscribe`**, **`SubscribeToVariableStructuralChanges`**. |
| `IMutableEntity<TDefinition>` | **`AddVariable` / `RemoveVariable`**, **`AddModifier`** → **`ModifierId`**, **`RemoveModifier(Variable, ModifierId)`**, **`ClearModifiers`**. |
| `ModifierId` | **`Guid`**-backed **`readonly struct`**; generated per **`AddModifier`** for stable removal alongside the **`Variable`** key. |
| `EntityModifierEntry` | Runtime **`Variable`** key + **`ModifierValue`** (authoring attaches editor tooling on serialized rows where applicable); **`Key`** property. |
| `EntityModifierEntryAsset` | `ScriptableObject` wrapping one **`EntityModifierEntry`** for editor authoring; explicit cast to **`EntityModifierEntry`** (C# allows only one user-defined conversion between these types). |
| `EntityComponent<TDefinition>` | Host; **`OnValidate`** (play mode) rebroadcasts effective bag edits to subscribers. |

**Runtime read boundary:** use **`GetValue` / `TryGetValue`**, **`GetVariable` / `TryGetVariable`**, and **`Subscribe`** on **`IReadOnlyEntity`** (or **`EntityInstance` / `EntityComponent`**). Do not rely on walking into serialized definition rows; **`Definition`**, **`Instance`**, and bag **`Entries`** are internal to **`Scaffold.Entities`**.

## Lookup semantics

- **Resolve by slot:** use **`Variable`** keys (implicit from **`VariableSO`** where convenient).
- **Three-bag chain:** definition → **`instanceBaseBag`** → **`instanceEffectiveBag`**.
- **Structural vs value:** base bag **`Add` / `Remove`** raise **`OnVariableStructuralChange`** (**`Added`** with value, **`Removed`** with null value); modifier recalculation uses silent effective writes and **`VariableNotifier`**.
- **Combine:** **`VariableModifierHandler`** layered on **`EntityVariableComputer`** (numeric sum for float/int; bool last wins; string concat).

## Breaking changes (serialization)

- **`VariableSO`** now serializes **`payloadTypeId`** as a **string** (e.g. `float`). Older assets that used **`valueType`** as an enum ordinal **will not deserialize correctly** — re-author those assets.
- **`Variable`** now serializes **`payloadTypeId`** instead of an enum **`type`** field. Inline serialized keys in YAML must use **`payloadTypeId`**.
- **`FloatVariableValue` / `IntVariableValue`** no longer expose **`Min` / `Max` / `Clamped`**; **`Combine`** uses plain sums only (no clamp). Model clamps with separate variables if needed.

## Adding a new payload type

1. Subclass **`VariableValue`** (concrete, non-abstract).
2. Add **`[VariableValueId("your-id")]`** with a stable, unique id.
3. Provide a **public parameterless constructor** (default struct fields ok).
4. Rebuild; the runtime registry discovers the type (see **`Runtime/link.xml`** + **`AlwaysLinkAssembly`** if IL2CPP stripping is aggressive).

## Testing

- `EntityInstanceTests`, **`VariableBagTests`**, **`VariableValueFactoryTests`** under `Tests/Runtime/`.
- Editor drawer registration: **`VariablePropertyDrawerEditorTests`**, **`EntityModifierEntryAssetEditorTests`** (modifier drawer + wrapper cast).

## Related

- `../../../Docs/App/AppStartup.md`, `../../../Architecture.md`.
- ExecPlan references under `Plans/`.
