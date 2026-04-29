# com.scaffold.entities

# Entities (Core)

## TL;DR

- Purpose: **Definition** / **instance** model with **`VariableSO`** identity and stable **`payloadTypeId`** strings (via **`[VariableValueId]`** on concrete **`VariableValue`** types), typed payloads (`FloatVariableValue`, …), **`Variable`** keys, **`IVariableBag` / `VariableBag`**, instance-only **modifiers** modeled as **`VariableModifier<T>`** subclasses with explicit **`Apply`** + **`Order`**, **`InstanceId`**, and **`EntityInstanceCreator<TDefinition>`**. Each instance uses a **three-bag chain**: definition **`VariableBag`** → **`instanceBaseBag`** → **`instanceEffectiveBag`** (modifier fold results). Reads use **`GetVariable<T>`** for the **inner** **`T`** on effective values.
- **Interfaces:** **`IReadOnlyEntity<TDefinition>`** (**`GetVariable<T>`** / **`TryGetVariable<T>`** for **inner** **`T`** reads, subscribe APIs) and **`IMutableEntity<TDefinition>`** ( **`AddVariable` / `RemoveVariable`**, **`AddModifier(EntityModifierEntry)`** returning **`ModifierId`**, removal by key + id). Internal **`EntityExtensions`** provides typed **`Subscribe`**, **`AddVariable<T>`**, **`AddModifier<T>`** helpers that map primitives to default modifiers (**`FloatAddModifier`**, …).
- **Location:** `Assets/Packages/com.scaffold.entities/Runtime/` — **`Core/`** and **`Behavior/`**. Assemblies: `Scaffold.Entities`, `Scaffold.Entities.Tests`, `Scaffold.Entities.Editor`, `Scaffold.Entities.Editor.Tests`, optional **`Scaffold.Entities.Samples`**.
- **Unity coupling:** References `UnityEngine`. See [Architecture.md](../../../Architecture.md).
- **Consumers:** Reference `Scaffold.Entities` from your module `.asmdef`.

## Folder layout (`Runtime/Core/`)

| Subfolder | Responsibility |
|-----------|----------------|
| **`Definitions/`** | `EntityDefinition`, `EntityDefinitionAsset`, `IEntityDefinition`, `IDefinitionVariableBagProvider`, `EntityModifierEntry`, `EntityModifierEntryAsset`. |
| **`Variables/`** | Keys and payloads: `Variable`, `VariableSO`, `VariableEntry`, `VariableValue` + **`Values/`**, `VariableValueIdAttribute`, `IVariableValue`. |
| **`VariableBags/`** | `IVariableBag`, `VariableBag`, `VariableStructuralChange` (parent chain for bases; structural notifications). |
| **`Instance/`** | `BaseEntityInstance`, `EntityInstance`, `EntityInstanceCreator`, `LocalVariableStorage`, `VariableModifierHandler`, `IEntityVariableStorage`. |
| **`Modifiers/`** | `VariableModifier`, `VariableModifier<T>`, concrete runtime modifiers (`FloatAddModifier`, …). |
| **`Contracts/`** | `IReadOnlyEntity`, `IMutableEntity`. |
| **`Identity/`** | `InstanceId`, id generators (`IInstanceIdGenerator`, `IncrementingInstanceIdGenerator`), `ModifierId`. |
| **`Subscriptions/`** | `VariableNotifier`, `CallbackDisposable`, `EmptyDisposable`. |
| **`Hosting/`** | `EntityComponent`, `EntityComponent<TDefinition>`. |
| **`Utilities/`** | `EntityExtensions`, `VariableValueFactory`, **`ModifierTypeIndex`**, **`VariableValueRegistry`** (all internal except extension entry points). |

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
| `VariableValue` | Abstract base for **base / storage cells**; concrete subclasses declare **`[VariableValueId("…")]`**; no combination logic (modifiers own **`Apply`**). |
| `VariableEntry` | Serializable definition row (used by **`VariableBag`** and Unity drawers); authoring fields are not part of the public read surface. |
| `IVariableBag` | Read: **`Parent`**, **`TryGetBase`**, **`LocalKeys`**. |
| `VariableBag` | Chained reads, **`Add` / `Remove`**, silent local writes for effective layer. |
| `EntityDefinition` | **`TryGetDefaultValue`**, **`AddVariable(Variable, VariableValue)`**; serialized variable rows are internal to the package. |
| `EntityInstance<TDefinition>` | Standalone mutable instance; inherits **`BaseEntityInstance<TDefinition>`**, implements **`IMutableEntity`**; **`NotifyAllEffectiveValues`** (editor / play-mode debug; internal to the runtime assembly). |
| `IReadOnlyEntity<out TDefinition>` | **`GetVariable<T>(Variable)`** / **`TryGetVariable<T>`** (inner **`T`** via **`IVariableValue<T>`** on the effective payload), **`Subscribe`**, **`SubscribeToVariableStructuralChanges`**. |
| `IMutableEntity<TDefinition>` | **`AddVariable` / `RemoveVariable`**, **`AddModifier`** → **`ModifierId`**, **`RemoveModifier(Variable, ModifierId)`**, **`ClearModifiers`**. |
| `ModifierId` | **`Guid`**-backed **`readonly struct`**; generated per **`AddModifier`** for stable removal alongside the **`Variable`** key. |
| `EntityModifierEntry` | Runtime **`Variable`** key + **`VariableModifier`** (**`[SerializeReference]`** concrete modifier). |
| `VariableModifier` / `VariableModifier<T>` | Non-generic root (**`Order`**) + **`Apply(T)`** on the inner type; concrete types encode the operation (add, multiply, bool override, string append, …). |
| `EntityModifierEntryAsset` | `ScriptableObject` wrapping one **`EntityModifierEntry`** for editor authoring; explicit cast to **`EntityModifierEntry`** (C# allows only one user-defined conversion between these types). |
| `EntityComponent<TDefinition>` | Host; **`OnValidate`** (play mode) rebroadcasts effective bag edits to subscribers. |

**Runtime read boundary:** use **`GetVariable<T>` / `TryGetVariable<T>`** with the **inner** type (e.g. **`float`**), **`Subscribe`**, and **`SubscribeToVariable`** on **`IReadOnlyEntity`**. Do not rely on walking into serialized definition rows; **`Definition`**, **`Instance`**, and bag **`Entries`** are internal to **`Scaffold.Entities`**.

## Lookup semantics

- **Resolve by slot:** use **`Variable`** keys (implicit from **`VariableSO`** where convenient).
- **Three-bag chain:** definition → **`instanceBaseBag`** → **`instanceEffectiveBag`**.
- **Structural vs value:** base bag **`Add` / `Remove`** raise **`OnVariableStructuralChange`** (**`Added`** with value, **`Removed`** with null value); modifier recalculation uses silent effective writes and **`VariableNotifier`**.
- **Modifiers:** each entry references a **`VariableModifier`** subclass. **`Order`** is ascending; ties keep **insertion** order. The handler **folds** **`Apply`** left-to-right by delegating to the typed wrapper — **`VariableValue<T>.ApplyModifiers`** sealed on the intermediate base recovers `T` via virtual dispatch, applies each modifier, and returns a fresh wrapper via the per-type **`WithValue`** override. No per-scalar branches in the handler, no boxing factory. **`EntityExtensions.AddModifier<T>`** still accepts primitives and maps them to additive / override / append modifiers.

### Adding a new modifier

1. Define **`public sealed class YourModifier : VariableModifier<float> { public override float Apply(float current) { … } }`** (use your inner type instead of **`float`** where appropriate) with **`[Serializable]`**, optional **`[SerializeField]`** operands, and a **parameterless** constructor for Unity.
2. It appears in **`ModifierTypeIndex`** automatically (closed **`VariableModifier<T>`** subclasses).
3. Default **`Order`**: use **`0`** for additive-style ops and **`100`** for multiplicative-style ops when you want multiply-after-add unless reordered.

## Breaking changes (state bridge dependency)

- **`ActiveModifier`** is now **public** so external mutators can hold ordered modifier stacks (for example `com.scaffold.entities.states`).
- **`VariableValue.ApplyModifiers(IReadOnlyList<ActiveModifier>)`** is now **public** for the same reason.
- **`InstanceId`** implements **`Scaffold.States.IReference`**. The **`Scaffold.Entities`** assembly references **`Scaffold.States`** for that marker interface only — do not add further `Scaffold.States` types inside `Scaffold.Entities`; use **`com.scaffold.entities.states`** for store integration.

## Breaking changes (serialization)

- **`VariableSO`** now serializes **`payloadTypeId`** as a **string** (e.g. `float`). Older assets that used **`valueType`** as an enum ordinal **will not deserialize correctly** — re-author those assets.
- **`Variable`** now serializes **`payloadTypeId`** instead of an enum **`type`** field. Inline serialized keys in YAML must use **`payloadTypeId`**.
- **`VariableValue.Combine`** is removed. External code that called it must use modifiers instead.
- **`IReadOnlyEntity.GetVariable<T>`** now returns the **inner** **`T`** (e.g. **`float`**), not a **`VariableValue`** wrapper. Call sites passing **`FloatVariableValue`** will fail at runtime; use **`GetVariable<float>`**.
- **`EntityModifierEntry`** serialized field is **`modifier`** (**`VariableModifier`**), not **`modifierValue`**. Older assets do not migrate — re-author sample **`EntityModifierEntryAsset`** rows in YAML.
- Implicit “payload type implies default combine” is gone: authors must pick **`FloatAddModifier`** vs **`FloatMultiplyModifier`**, **`BoolOverrideModifier`**, **`StringAppendModifier`**, etc.
- Concrete **`VariableValue`** types may expose **`(T)`** constructors used when materializing effective bag cells (additive API change only).
- **`FloatVariableValue` / `IntVariableValue`** no longer expose **`Min` / `Max` / `Clamped`** (unchanged from prior releases); model clamps with separate variables if needed.

## Adding a new payload type

1. Subclass **`VariableValue`** (concrete, non-abstract).
2. Add **`[VariableValueId("your-id")]`** with a stable, unique id.
3. Provide a **public parameterless constructor** (default struct fields ok).
4. Rebuild; the runtime registry discovers the type (see **`Runtime/link.xml`** + **`AlwaysLinkAssembly`** if IL2CPP stripping is aggressive).

## Testing

- `EntityInstanceTests`, **`ModifierTypeIndexTests`**, **`VariableBagTests`**, **`VariableValueFactoryTests`** under `Tests/Runtime/`.
- Editor drawer registration: **`VariablePropertyDrawerEditorTests`**, **`EntityModifierEntryAssetEditorTests`** (modifier drawer + wrapper cast).

## Related

- `../../../Docs/App/AppStartup.md`, `../../../Architecture.md`.
- ExecPlan references under `Plans/`.
