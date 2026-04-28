# Decouple Entities from Unity and make them State-compatible

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

## Purpose / Big Picture

Today the `com.scaffold.entities` package is tightly coupled to Unity in two ways: `EntityDefinition` inherits `ScriptableObject`, and `EntityModifierEntry` carries a `VariableSO` (Unity asset) reference as part of its key. This makes entities unusable in pure-simulation contexts and incompatible with `com.scaffold.states`, because modifier application happens synchronously inside `EntityInstance` and bypasses the Store's transactional mutation pipeline entirely.

The practical consequence is that any game requiring a full state history — undo/redo, deterministic replay, snapshot save/restore — cannot use entities for runtime data without building a parallel variable system. A card game framework where every card mutation must flow through the same pipeline as every card movement is a concrete example: today that is impossible without significant glue code.

After this work, a developer can choose between two modes for any entity. In standalone mode, the entity behaves exactly as it does today: fast, self-contained, no external dependencies. In state-backed mode, the entity's variable values and modifier contributions live in the Store as canonical slices; every write goes through a Mutator and participates in history, undo/redo, and snapshots automatically. The read API is identical in both modes — code that only reads from an entity does not need to know which mode is active.

You can see this working when: a test creates a `StateEntity` backed by a real `Store`, applies a modifier via `store.Execute(new AddModifierPayload(...))`, saves a snapshot, applies a second modifier, restores the snapshot, and asserts that the entity's variable value returns to the pre-second-modifier state as read through the normal `GetValue<T>` API.

## Progress

- Author initial ExecPlan at `Plans/EntitiesStateBridge/EntitiesStateBridge-ExecPlan.md` (this file).
- **Milestone 1 — Unity Decoupling (core deliverables landed in package):**
  - `Variable`/`VariableEntry`/`EntityModifierEntry` keys are `**Variable`** (serializable `**class**`, not positional record — Unity Serialization does not cooperate with boxed `SerializedProperty` access for records).
  - `IEntityDefinition`; plain C# `EntityDefinition`; `EntityDefinitionAsset` (`ScriptableObject`) implements the interface; interface constraints use `where TDefinition : IEntityDefinition`.
  - `VariableBag` / `EntityInstance` / `EntityComponentT` / samples and tests updated for the new model.
  - **Editor authoring path:** `#if UNITY_EDITOR` `**variableAuthoring`** (`VariableSO`) on `VariableEntry` / `EntityModifierEntry` for stable ObjectField binding; inline `Variable` + legacy `variableLegacy` still supported; `VariableKeySoField` writes inline key members from SO pickers; `**VariableValueFactory.CreateDefault**` + `**RebaseSerialized*PayloadIfMismatch**` keep `**SerializeReference**` payloads aligned when the variable **type** changes (e.g. float → bool).
  - **Editor robustness:** `ApplyModifiedProperties` invalidates cached `SerializedProperty` handles — drawers **re-resolve** `.value` and `**RefreshExtraPathsAfterSerializeReferencePayloadChanged`** before drawing expanded/`PropertyField` UI.
  - **Tooling slim-down:** Removed AssetDatabase “find VariableSO by key name” display fallback; `**ResolveSoForDisplay`** = `**variableAuthoring` | `variableLegacy**` only; inlined `**key**` / `**type**` SerializedProperty lookup for `**Variable**` (removed multi-name probe helpers).
  - **Definition API:** `**AddVariable(Variable, VariableValue)`** only on `**EntityDefinition**` and `**EntityDefinitionAsset**` — no `**AddVariable(VariableSO, ...)**` overload; call sites with a `**VariableSO**` use `**(Variable)so**` (or `**VariableSO**`→`**Variable**` conversion) when calling `**AddVariable**`.
- **Milestone 2 — Internal Restructuring (core deliverables landed in package):** `ModifierId` (`Guid`-backed `readonly struct`), `VariableModifierHandler` keyed by `ModifierId` with stable insertion order per variable list, `**EntityVariableComputer`**, `**IEntityVariableStorage**`, `**LocalVariableStorage**`, `**BaseEntityInstance<TDefinition>**`, `**IMutableEntity<TDefinition>**` (replaced `**IEntity**` / `**IInstance**`), `**EntityInstance**` delegates to `**LocalVariableStorage**`; `**RemoveModifier(Variable, ModifierId)**`.
- Milestone 3 — Extension Package: **not started** (`com.scaffold.entities.states`, Store bridge, Mutators — pending).

## Surprises & Discoveries

- **SerializedProperty + positional records**: Using `SerializedProperty.boxedValue` / struct-style accessors on `**Variable`** backed by positional record shapes caused `**InvalidOperationException**` until `**Variable**` became an explicit `**[Serializable] class**` with `**[SerializeField]**` fields (`**key**`, `**type**`).
- **ApplyModifiedProperties invalidates sibling properties**: Calling `**ApplyModifiedProperties`** from the VariableSO `**ObjectField**` handler while still holding `**modifierValue.value` / `baseValue.value**` from an earlier `**FindPropertyRelative**` caused `**ObjectDisposedException**`. Fix: `**SerializedObject.Update()**` and **re-query** paths after apply (see `**ResolveValueNestedPropertyAfterApply`** and drawer refresh helpers).
- **Type change without new `VariableValue` instance**: Changing `**VariableSO`** while leaving the old `**FloatVariableValue**` (etc.) under `**SerializeReference**` left the inspector showing the wrong payload type until **rebase** logic replaced the managed reference with `**VariableValueFactory.CreateDefault(expectedType)`**.

## Decision Log

- Decision: Unity decoupling and state integration are delivered as three sequential milestones rather than one. Milestone 1 (decoupling) is a prerequisite for Milestone 2 (restructuring), which is a prerequisite for Milestone 3 (extension package). Each milestone leaves the package in a compilable, fully working state.
Rationale: Keeps diffs reviewable and allows stopping after Milestone 1 or 2 if state integration is not yet needed. Avoids a single giant refactor that is hard to validate in parts.
Author: Design session, 2026-04-27.
- Decision: The existing `EntityInstance` standalone behavior is preserved exactly. No existing call sites change their behavior. The state-backed variant is a new type (`StateEntity`) in a new package, not a mode flag on the existing class.
Rationale: No consumers exist today, but the design should be safe if consumers are added later. Changing the behavior of `EntityInstance` would break any project using it as-is. A separate type makes the trade-off explicit at the call site.
Author: Design session, 2026-04-27.
- Decision: `ModifierId` uses `System.Guid` as its underlying identity rather than a sequential integer.
Rationale: Guids are stable across serialization, network transport, and process restarts. A sequential allocator requires a shared counter, which is not safe in headless or multi-instance contexts.
Author: Design session, 2026-04-27.
- Decision: `IEntityDefinition` is introduced as a plain C# interface. All generic constraints in the entities package change from `where TDefinition : EntityDefinition` (which required a ScriptableObject subclass) to `where TDefinition : IEntityDefinition`. Both the new pure-C# `EntityDefinition` class and the new `EntityDefinitionAsset` ScriptableObject wrapper implement this interface.
Rationale: Allows simulation code to hold an `EntityDefinition` created entirely in code without touching the Unity asset system, while the inspector authoring path continues to use `EntityDefinitionAsset` ScriptableObjects unchanged.
Author: Design session, 2026-04-27.
- Decision: `EntityVariableState` (the Store slice for a state-backed entity) stores base values, the full modifier stack keyed by `ModifierId`, and the computed effective values all in one record rather than splitting modifiers into a separate canonical slice and effective values into an aggregate.
Rationale: For the primary use case (card games, chess-style simulations), a single atomic record per entity is simpler to reason about and snapshot. The aggregate model would require two Store registrations per entity and a wiring step. This can be revisited if a project needs to query aggregate stats across all entities of a given type.
Author: Design session, 2026-04-27.
- Decision: `EffectiveValues` is stored inline in `EntityVariableState` rather than being computed on every read.
Rationale: Computing effective values requires iterating the modifier stack on every read call. Since reads are hot-path (called every frame by UI, AI, etc.), caching the computed result in the record trades slightly larger snapshot size for consistent read performance. The value is recomputed only when the state changes, which happens inside the mutator.
Author: Design session, 2026-04-27.
- Decision: The caller pre-generates the `ModifierId` before dispatching `AddModifierPayload` to the store, rather than having the mutator generate it and somehow return it.
Rationale: Mutators in `com.scaffold.states` return a new state record. They cannot return a secondary value. The caller generating the ID upfront — `var id = ModifierId.New(); store.Execute(new AddModifierPayload(..., id));` — keeps the mutator pure and gives the caller a stable reference to use for later removal.
Author: Design session, 2026-04-27.
- Decision: `IEntityVariableStorage` is a read-only interface. It has no write methods. Write operations belong to the concrete storage implementations (`LocalVariableStorage` for standalone, `store.Execute(...)` for state-backed) and are not part of the shared contract.
Rationale: `BaseEntityInstance` only needs to read from storage to implement its read surface. Expressing write operations on the storage interface would mean `StateEntity` would have to either implement them or throw, reintroducing the problem we are trying to avoid. The type system enforces the correct pattern: code that needs to write must hold either an `IMutableEntity` (standalone) or a `Store` reference (state-backed).
Author: Design session, 2026-04-27.
- Decision: `VariableEntry` stores a `**Variable**` key (inline serialization), not a `**VariableSO**` as the runtime key. **Runtime / player data stays SO-free.** For **editor authoring**, optional `**variableAuthoring`** (`#if UNITY_EDITOR` `[SerializeField] VariableSO`) binds the picker; `**variableLegacy**` supports migration from former `variable` serialization. `**ResolveSoForDisplay**` uses `**variableAuthoring` | `variableLegacy**` — **no AssetDatabase sweep by name for display** (removed to reduce tooling and cost; orphaned rows without either reference show `**None`** until assigned).
Rationale: ScriptableObjects remain authoring-facing; the authoritative key for gameplay is `**Variable**`. Explicit editor-only `**variableAuthoring**` gives a stable Object reference without implying SO in player builds (`UNITY_EDITOR`-stripped field). Name-based `**FindAssets**` was dropped as redundant once authoring ref + inline fill existed.
Author: Scaffold.Entities iteration, 2026-04-27–2026-04 (amends 2026-04-27 design session text that assumed name-only resolution).
- Decision: `**EntityDefinition` and `EntityDefinitionAsset` do not expose `AddVariable(VariableSO, VariableValue)**`. Only `**AddVariable(Variable key, VariableValue defaultValue)**` is part of the API. Code that has a `VariableSO` calls `**AddVariable((Variable)so, defaultValue)**` (or equivalent conversion) at the call site.
Rationale: Avoids an SO-typed surface on definition types; keeps authoring keys purely `Variable`-typed in method signatures while `VariableSO` remains available for editor pickers and asset references.
Author: Scaffold.Entities iteration, 2026-04-27.

## Outcomes & Retrospective

To be written at completion.

## Context and Orientation

This section explains the relevant parts of both packages as they exist today. Read it fully before touching any code.

### The entities package (`Assets/Packages/com.scaffold.entities/`)

The entities package models gameplay objects (characters, items, cards) as definitions and instances. A **definition** describes a kind of entity — what variables it has and what their default values are. An **instance** represents one live entity bound to exactly one definition. Instances can have **modifiers** applied to them: temporary or permanent adjustments to variable values.

The key types and their current file locations are:

`Runtime/Core/Variables/Variable.cs` — `public record Variable(string Key, VariableValueType Type)`. This is a plain C# record used as a dictionary key. It already has no Unity dependency.

`Runtime/Core/Variables/VariableSO.cs` — `public class VariableSO : ScriptableObject`. A Unity asset that acts as the inspector-facing identity for a variable. It has an `implicit operator Variable(VariableSO so)` conversion so Unity asset references can be used wherever a `Variable` record is expected. Today this SO is used as the primary runtime key in several places that should instead use the plain `Variable` record.

`Runtime/Core/Variables/VariableEntry.cs` — `public sealed class VariableEntry`. A serializable pair of (`VariableSO variable`, `VariableValue baseValue`). Used inside `VariableBag` to define the default values on a definition. The `VariableSO` field is the key — this is one of the coupling points we will fix.

`Runtime/Core/Variables/VariableValue.cs` and its four concrete subtypes in `Runtime/Core/Variables/Values/` — the abstract base `VariableValue` and `FloatVariableValue`, `IntVariableValue`, `BoolVariableValue`, `StringVariableValue`. Each concrete subtype implements `Combine(IReadOnlyList<VariableValue>)` which is the combination rule when modifiers are applied.

`Runtime/Core/VariableBags/VariableBag.cs` — `public sealed class VariableBag`. A chained dictionary. A bag can have a parent bag; when a key is not found locally it falls through to the parent. `EntityInstance` uses three bags chained together: definition defaults → instance base values → effective values with modifiers applied.

`Runtime/Core/EntityDefinition.cs` — `public class EntityDefinition : ScriptableObject`. The definition ScriptableObject. Holds a `VariableBag bag` populated in the Unity inspector. Has `OnEnable` and `OnValidate` Unity lifecycle methods. The `ScriptableObject` inheritance is one of the two main coupling points this plan fixes.

`Runtime/Core/EntityModifierEntry.cs` — `public sealed class EntityModifierEntry`. A serializable pair of (key, `VariableValue modifierValue`). Currently has two key fields: `[SerializeField] VariableSO variable` and `[NonSerialized] Variable? variableKey`. The `Key` property returns `variableKey ?? (Variable)variable`. This dual-path is confusing and leaks Unity SO references into otherwise pure code. This plan removes the `VariableSO` path.

`Runtime/Core/EntityInstance.cs` — `public class EntityInstance<TDefinition> : IInstance<TDefinition> where TDefinition : EntityDefinition`. The main runtime entity class. Internally owns `instanceBaseBag`, `instanceEffectiveBag`, a `VariableModifierHandler`, and a `VariableNotifier`. The `AddModifier(EntityModifierEntry)` method applies the modifier immediately and synchronously — it calls `RecalculateAndNotify` before returning, which fires subscribers. There is no way to intercept, defer, or stage this operation. This is the root of the state incompatibility.

`Runtime/Core/VariableModifierHandler.cs` — `internal sealed class VariableModifierHandler`. Stores `Dictionary<Variable, List<EntityModifierEntry>>` and implements the combination loop. Today its `GetEffective` method is internal and inaccessible from outside the package.

`Runtime/Core/IReadOnlyEntity.cs`, `Runtime/Core/IEntity.cs`, `Runtime/Core/IInstance.cs` — three public interfaces in a hierarchy. `IReadOnlyEntity` is the read surface. `IEntity` extends it with `AddVariable`/`RemoveVariable`. `IInstance` extends `IEntity` with `AddModifier`/`RemoveModifier`/`ClearModifiers`. All three have `where TDefinition : EntityDefinition`, which requires a ScriptableObject subclass.

### The states package (`Assets/Packages/com.scaffold.states/`)

The states package implements an immutable, transactional store for game state. The key ideas are:

A **State** is an immutable C# `record`. State values are never mutated in place; instead, a new record is produced with the changed fields.

A **Store** is the central object that holds all canonical state. It is the single source of truth. You read from it with `store.Get<TState>()` or `store.Get<TState>(reference)`. A **reference** (implementing `IReference`) is a key that allows multiple independent instances of the same state type — for example, one `EntityVariableState` per entity, each keyed by the entity's `InstanceId`.

A **Mutator** is an object that produces a new state from an old state. Mutators are pure functions: they take the current state and a payload, and return a new state. They never write to external objects or fire side effects. The Store applies mutators atomically via a scratchpad overlay — the new state is not committed until the entire mutation batch succeeds.

A **snapshot** is a serializable copy of all canonical slices. `store.SaveSnapshot()` captures the full state of all registered slices. `store.LoadSnapshot(snapshot)` restores it, which is the mechanism for undo/redo, save/load, and deterministic replay.

The key incompatibility is this: `EntityInstance.AddModifier()` writes to internal state and fires subscriber callbacks immediately, outside any Store mutation context. From the Store's perspective, nothing happened — the modifier is invisible to the snapshot system, to the event deduplication system, and to any undo pipeline. Calling `AddModifier` inside a Mutator would be incorrect and would cause double-notification if the Store also fired its own subscription events.

### What this plan builds

Milestone 1 removes the Unity coupling from the entities package without changing any runtime behavior. `EntityDefinition` becomes a plain C# class. `EntityModifierEntry` uses only the `Variable` record as its key. A new `IEntityDefinition` interface allows both the new C# definition and the new ScriptableObject wrapper to be used interchangeably.

Milestone 2 restructures the entities package internals. The combination logic becomes a public static function. An `IEntityVariableStorage` interface abstracts where values are stored. A `BaseEntityInstance` abstract class carries the read surface that all entity variants share. `EntityInstance` becomes a subclass that adds write methods. A `ModifierId` record gives each modifier a stable identity so it can be removed by reference rather than by object equality.

Milestone 3 creates a new bridge package `com.scaffold.entities.states` that depends on both core packages. It contains the `StateEntity` type, the `EntityVariableState` Store slice, and the Mutators that implement modifier and variable changes through the Store pipeline. Neither core package gains a dependency on the other.

## Plan of Work

### Milestone 1 — Unity Decoupling

The goal of this milestone is to remove the hard Unity coupling from the runtime entity data model. After this milestone, `EntityDefinition` no longer inherits `ScriptableObject`, `EntityModifierEntry` no longer carries a `VariableSO` field, and every interface constraint no longer requires a ScriptableObject subclass. The existing standalone runtime behavior is unchanged — `EntityInstance` with a `EntityDefinitionAsset` works exactly as before.

**Step 1.1 — Clean up `VariableEntry` and `EntityModifierEntry` to use `Variable` records as keys.**

Open `Runtime/Core/Variables/VariableEntry.cs`. Replace the `[SerializeField] private VariableSO variable` field with `[SerializeField] private Variable key`. Remove the internal `VariableSO Variable` property and replace it with `internal Variable Key => key`. Remove the `internal VariableEntry(VariableSO variable, VariableValue baseVal)` constructor — the new constructor takes a `Variable` directly: `internal VariableEntry(Variable key, VariableValue baseVal)`. Remove the `Create(VariableSO, VariableValue)` factory method and replace it with `Create(Variable key, VariableValue baseVal)`. Remove the `EnsureValueMatchesType()` method — it validated that the `VariableSO` type matched the value type, but the `Variable` record already carries its `VariableValueType` and the value type can be validated against that directly when needed. The resulting `VariableEntry` has two fields: `Variable key` and `VariableValue baseValue`, both plain C# serializable types with no Unity asset references.

Open `Runtime/Core/VariableBags/VariableBag.cs`. In `RebuildCache()`, replace the `(Variable)entry.Variable` cast with `entry.Key` — the entry already holds a `Variable` record and no conversion is needed. This removes the last place where the bag silently depended on SO-to-record conversion at rebuild time.

Open `Runtime/Core/EntityModifierEntry.cs`. Remove the `[SerializeField] private VariableSO variable` field. Remove the constructor overload `EntityModifierEntry(VariableSO variable, VariableValue modifierValue)`. Remove the `public VariableSO Variable` property. Remove the null-coalescing resolution in the `Key` property so it becomes simply `public Variable Key => variableKey`. Rename the backing field from `variableKey` to `key` for clarity. The `[NonSerialized]` attribute is removed because `key` is now the only key field and serialization is straightforward. The resulting class has two fields: `Variable key` and `VariableValue modifierValue`. The `EntityModifierEntryAsset` ScriptableObject wrapper at `Runtime/Core/EntityModifierEntryAsset.cs` still holds a `[SerializeField] VariableSO variable` internally for its own inspector use; update it to convert on read: `new EntityModifierEntry((Variable)variable, modifierValue)`.

The property drawer for `VariableEntry` (`Editor/VariableBagPropertyDrawer.cs`) must be updated to reflect the new backing field. Since `key` is now a `Variable` record (a plain serializable struct with a `Key` string and a `VariableValueType`), the drawer can no longer simply show a `VariableSO` object field by serializing the SO reference. Instead it resolves the SO for display: at draw time it calls `AssetDatabase.FindAssets("t:VariableSO")` filtered by name match against `key.Key`, renders an `EditorGUI.ObjectField` showing the resolved SO (or null if none found), and on assignment converts the dropped SO to a `Variable` record by writing `(Variable)droppedSO` back into the `key` serialized field. This keeps the inspector experience identical — designers drag `VariableSO` assets onto entries — while the serialized backing data is always a pure `Variable` record with no SO reference.

**Step 1.2 — Introduce `IEntityDefinition`.** Create a new file `Runtime/Core/IEntityDefinition.cs` in the entities package. This is a plain C# interface with no Unity dependencies:

```
namespace Scaffold.Entities
{
    public interface IEntityDefinition
    {
        bool TryGetDefaultValue(Variable key, out VariableValue value);
        System.Collections.Generic.IEnumerable<Variable> DefinedVariables { get; }
    }
}
```

`TryGetDefaultValue` returns the base value for a variable key as declared in the definition. `DefinedVariables` enumerates the keys that have defaults. These are the only two things `BaseEntityInstance` needs from a definition at runtime.

**Step 1.3 — Make `EntityDefinition` a plain C# class.** Open `Runtime/Core/EntityDefinition.cs`. Remove `: ScriptableObject`. Remove `OnEnable()` and `OnValidate()` — these are Unity lifecycle callbacks that only apply to ScriptableObjects. Remove any legacy `AddVariable(VariableSO variable, VariableValue defaultValue)` overload if it still exists. Expose `**AddVariable(Variable key, VariableValue defaultValue)`** only (no `VariableSO` overload on `EntityDefinition` or `EntityDefinitionAsset`). Call sites that hold a `VariableSO` pass `**(Variable)so**` (or use `VariableSO`’s conversion to `Variable`, if defined) when calling `**AddVariable**`. Implement `IEntityDefinition`: `TryGetDefaultValue` delegates to `bag.TryGetBase(key, out value)`, and `DefinedVariables` returns `bag.LocalKeys`. The class no longer has any `using UnityEngine` imports.

**Step 1.4 — Create `EntityDefinitionAsset`.** Create a new file `Runtime/Core/EntityDefinitionAsset.cs`. This is the Unity ScriptableObject that holds a definition for inspector authoring. It does not inherit from `EntityDefinition` — it is a flat ScriptableObject that implements `IEntityDefinition` directly. Its serialized `VariableBag` uses `VariableEntry` objects whose keys are `Variable` records (not SO references), exactly as updated in Step 1.1. Unity lifecycle methods live here. `**AddVariable` takes a `Variable` only** — do not add a `VariableSO` overload; callers use `**(Variable)so`** at the call site when needed.

```
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entity/Definition", fileName = "EntityDefinition")]
    public class EntityDefinitionAsset : ScriptableObject, IEntityDefinition
    {
        [SerializeField] private VariableBag bag = new VariableBag();

        internal IReadOnlyList<VariableEntry> Entries => bag.Entries;
        internal VariableBag Bag => bag;

        private void OnEnable() => bag.RebuildCache();
        private void OnValidate() => bag.RebuildCache();

        public bool TryGetDefaultValue(Variable key, out VariableValue value)
            => bag.TryGetBase(key, out value);

        public IEnumerable<Variable> DefinedVariables => bag.LocalKeys;

        public void AddVariable(Variable key, VariableValue defaultValue)
        {
            bag.AddSerializedEntry(VariableEntry.Create(key, defaultValue));
            bag.RebuildCache();
        }

        internal void RebuildLookup() => bag.RebuildCache();
    }
}
```

The key point: the serialized bag contains `VariableEntry` objects with `Variable` keys. There is no SO reference anywhere in the serialized definition data. The `OnValidate` no longer calls `EnsureValueMatchesType` on each entry because that method previously validated the SO's declared type against the value type — with `Variable` as keys, the type is embedded in the key and can be validated directly against `entry.BaseValue.Type` if needed without SO involvement.

**Step 1.5 — Update all interface constraints.** Open `Runtime/Core/IReadOnlyEntity.cs`, `Runtime/Core/IEntity.cs`, and `Runtime/Core/IInstance.cs`. In each file, change `where TDefinition : EntityDefinition` to `where TDefinition : IEntityDefinition`. The `out` covariance modifier on `TDefinition` in `IReadOnlyEntity` is preserved.

**Step 1.6 — Update `EntityInstance` constraint.** Open `Runtime/Core/EntityInstance.cs`. Change `where TDefinition : EntityDefinition` to `where TDefinition : IEntityDefinition`. The `WireBagParentsToDefinition` method currently calls `entityDefinition.Bag` to wire the instance bags to the definition bag as parent. Because `IEntityDefinition` does not expose `Bag` (that is an implementation detail, not a read contract), add `internal VariableBag Bag` to both `EntityDefinition` and `EntityDefinitionAsset` and keep the bag wiring logic unchanged. The interface constraint relaxation does not affect this wiring path since both concrete definition types expose the bag internally.

**Step 1.7 — Update `EntityComponent` and `EntityComponentT`.** Open `Runtime/Core/EntityComponent.cs` and `Runtime/Core/EntityComponentT.cs`. Change the `TDefinition` constraint from `EntityDefinition` to `IEntityDefinition` to match the updated interfaces. These MonoBehaviour wrappers hold a `[SerializeField] TDefinition definition` — since `EntityDefinitionAsset` is still a ScriptableObject, Unity serialization continues to work for the inspector path when `TDefinition` is `EntityDefinitionAsset`. The constraint relaxation does not break serialization.

After Milestone 1, run `validate-changes.cmd` and confirm zero diagnostics. The package should compile cleanly with the new `EntityDefinitionAsset` wrapping the Unity authoring path and the pure `EntityDefinition` available for code-only use.

---

### Milestone 2 — Internal Restructuring

The goal of this milestone is to restructure the internals of the entities package so that a subclass can supply its own storage backend without inheriting the standalone mutation logic. After this milestone, `EntityInstance` is a subclass of `BaseEntityInstance` that adds write methods using `LocalVariableStorage`. The bridge package (Milestone 3) will subclass `BaseEntityInstance` with `StoreVariableStorage` instead.

**Step 2.1 — Add `ModifierId`.** Create `Runtime/Core/ModifierId.cs`:

```
using System;

namespace Scaffold.Entities
{
    public record ModifierId(Guid Id)
    {
        public static ModifierId New() => new ModifierId(Guid.NewGuid());
    }
}
```

This gives each applied modifier a stable, globally unique identity. The caller generates a `ModifierId` before applying the modifier and holds it for later removal — the same pattern used by `InstanceId`.

**Step 2.2 — Update `VariableModifierHandler` to use `ModifierId`.** Open `Runtime/Core/VariableModifierHandler.cs`. Change the storage from `Dictionary<Variable, List<EntityModifierEntry>>` to `Dictionary<Variable, Dictionary<ModifierId, EntityModifierEntry>>`. Change `AddModifier(EntityModifierEntry entry)` to `ModifierId AddModifier(EntityModifierEntry entry)`: it generates a `ModifierId.New()` internally, stores it as the dictionary key, and returns it. Change `RemoveModifier(EntityModifierEntry entry)` to `bool RemoveModifier(Variable key, ModifierId id)`: look up the inner dictionary by variable key, then remove by `ModifierId`. Remove the old object-equality removal path entirely. Update `GetEffective`, `HasModifiersFor`, `ClearModifiersForKey`, and `ModifiedVariables` to match the new storage shape — the combination logic itself (`FillScratch` calling `baseValue.Combine(scratch)`) does not change.

**Step 2.3 — Extract `EntityVariableComputer`.** Create `Runtime/Core/EntityVariableComputer.cs`. This is a public static class that exposes the combination logic currently locked inside `VariableModifierHandler`:

```
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public static class EntityVariableComputer
    {
        public static VariableValue ComputeEffective(
            VariableValue baseValue,
            IReadOnlyList<VariableValue> contributions)
        {
            if (baseValue == null) return null;
            if (contributions == null || contributions.Count == 0) return baseValue;
            return baseValue.Combine(contributions);
        }
    }
}
```

`VariableModifierHandler.GetEffective` is updated to delegate to `EntityVariableComputer.ComputeEffective`. The bridge package's Mutators call `EntityVariableComputer.ComputeEffective` directly without replicating the combination logic.

**Step 2.4 — Create `IEntityVariableStorage`.** Create `Runtime/Core/IEntityVariableStorage.cs`:

```
using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public interface IEntityVariableStorage
    {
        bool TryGetEffective(Variable key, out VariableValue value);
        bool TryGetBase(Variable key, out VariableValue value);
        IEnumerable<Variable> Variables { get; }
        IDisposable Subscribe(Variable key, Action<VariableValue> callback);
        void Unsubscribe(Variable key, Action<VariableValue> callback);
        IDisposable SubscribeToVariableAdded(Action<Variable, VariableValue> onAdded);
        IDisposable SubscribeToVariableRemoved(Action<Variable> onRemoved);
    }
}
```

This interface is read-only by design. It describes everything `BaseEntityInstance` needs to implement its read surface. It does not include any write methods — write paths differ between standalone and state-backed storage and are not part of the shared contract.

**Step 2.5 — Create `LocalVariableStorage`.** Create `Runtime/Core/LocalVariableStorage.cs`. This class extracts everything that is currently embedded in `EntityInstance` for the standalone path: the two runtime bags (`instanceBaseBag`, `instanceEffectiveBag`), the `VariableModifierHandler`, and the `VariableNotifier`. It implements `IEntityVariableStorage`. It also exposes write methods for use by `EntityInstance`:

```
ModifierId AddModifier(EntityModifierEntry entry)
bool RemoveModifier(Variable key, ModifierId id)
void ClearModifiers()
IEnumerable<Variable> ModifiedVariables
bool AddVariable(Variable key, VariableValue initialBase)
bool RemoveVariable(Variable key)
```

The initialization method `WireToDefinition(IEntityDefinition definition)` replaces the current `WireBagParentsToDefinition` in `EntityInstance`. It seeds the base bag parent from the definition's bag (for the `EntityDefinitionAsset` path) or from the definition's own `TryGetDefaultValue` implementation (for the pure-C# path). The `RecalculateAndNotify` method moves here from `EntityInstance`.

**Step 2.6 — Create `BaseEntityInstance`.** Create `Runtime/Core/BaseEntityInstance.cs`. This is an abstract class that holds the definition reference, the storage interface, and the entire read surface:

```
public abstract class BaseEntityInstance<TDefinition> : IReadOnlyEntity<TDefinition>
    where TDefinition : IEntityDefinition
{
    protected TDefinition Definition { get; private set; }
    protected IEntityVariableStorage Storage { get; private set; }

    protected virtual void Initialize(TDefinition definition, IEntityVariableStorage storage)
    {
        Definition = definition;
        Storage = storage;
    }

    // IReadOnlyEntity<TDefinition> — full implementation, delegates to Storage
    public T GetValue<T>(Variable key) { ... }
    public bool TryGetValue<T>(Variable key, out T value) { ... }
    public TVar GetVariable<TVar>(Variable key) where TVar : VariableValue { ... }
    public bool TryGetVariable<TVar>(Variable key, out TVar value) where TVar : VariableValue { ... }
    public IDisposable Subscribe(Variable key, Action<VariableValue> onChange)
        => Storage.Subscribe(key, onChange);
    public void Unsubscribe(Variable key, Action<VariableValue> onChange)
        => Storage.Unsubscribe(key, onChange);
    public IDisposable SubscribeToVariableAdded(Action<Variable, VariableValue> onAdded)
        => Storage.SubscribeToVariableAdded(onAdded);
    public IDisposable SubscribeToVariableRemoved(Action<Variable> onRemoved)
        => Storage.SubscribeToVariableRemoved(onRemoved);
}
```

The `InstanceId Id` property remains on `BaseEntityInstance` as well, since it is part of `IReadOnlyEntity`. It is set during initialization.

**Step 2.7 — Simplify the interface hierarchy.** The current three-level hierarchy (`IReadOnlyEntity` → `IEntity` → `IInstance`) splits write methods across two layers for historical reasons that no longer apply. Replace it with two levels.

`IReadOnlyEntity<out TDefinition>` (already exists — update constraint only, contents unchanged).

`IMutableEntity<TDefinition>` (new file `Runtime/Core/IMutableEntity.cs`) replaces both `IEntity` and `IInstance`. It extends `IReadOnlyEntity<TDefinition>` and adds all write methods:

```
bool AddVariable(Variable key, VariableValue initialBase);
bool RemoveVariable(Variable key);
ModifierId AddModifier(EntityModifierEntry entry);
bool RemoveModifier(Variable key, ModifierId id);
void ClearModifiers();
```

Delete `Runtime/Core/IEntity.cs` and `Runtime/Core/IInstance.cs`.

**Step 2.8 — Refactor `EntityInstance`.** `EntityInstance<TDefinition>` now extends `BaseEntityInstance<TDefinition>` and implements `IMutableEntity<TDefinition>`. Its constructor or `Initialize` method creates a `LocalVariableStorage`, wires it to the definition, and calls `base.Initialize(definition, storage)`. All the bag fields, modifier handler, and notifier are removed from `EntityInstance` — they live in `LocalVariableStorage` now. Write methods on `EntityInstance` cast `Storage` to `LocalVariableStorage` (safe, because this class always constructs with that type) and delegate. The `EntityInstance.Initialize(InstanceId, TDefinition)` signature is preserved for compatibility with `EntityInstanceCreator`.

After Milestone 2, run `validate-changes.cmd`. The package surface should be identical to before from a consuming perspective: the read API through `IReadOnlyEntity` is unchanged, modifier removal now uses `(Variable, ModifierId)` instead of object reference, and `EntityVariableComputer.ComputeEffective` is newly public.

---

### Milestone 3 — Extension Package

The goal of this milestone is to create `com.scaffold.entities.states`, a package that bridges entities and states without modifying either. After this milestone, a developer can create a `StateEntity` backed by a `Store`, apply modifiers through `store.Execute(...)`, and have snapshot round-trips fully restore all entity variable values.

**Step 3.1 — Create the package scaffold.** Create the directory `Assets/Packages/com.scaffold.entities.states/`. Create the asmdef at `Runtime/Scaffold.Entities.States.asmdef` referencing `Scaffold.Entities` and `Scaffold.States`. Create `package.json` with name `com.scaffold.entities.states`. Create a `Tests/` directory and a `Scaffold.Entities.States.Tests.asmdef` referencing the runtime assembly, `Scaffold.Entities`, `Scaffold.States`, and `nunit.framework`.

**Step 3.2 — Create `EntityVariableState`.** Create `Runtime/EntityVariableState.cs`. This record is the Store slice that holds all variable data for one entity:

```
using System.Collections.Generic;
using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public record EntityVariableState(
        System.Collections.Immutable.ImmutableDictionary<Variable, VariableValue> BaseValues,
        System.Collections.Immutable.ImmutableDictionary<Variable, System.Collections.Immutable.ImmutableDictionary<ModifierId, EntityModifierEntry>> ModifierStacks,
        System.Collections.Immutable.ImmutableDictionary<Variable, VariableValue> EffectiveValues
    ) : State
    {
        public static EntityVariableState Empty() =>
            new EntityVariableState(
                System.Collections.Immutable.ImmutableDictionary<Variable, VariableValue>.Empty,
                System.Collections.Immutable.ImmutableDictionary<Variable, System.Collections.Immutable.ImmutableDictionary<ModifierId, EntityModifierEntry>>.Empty,
                System.Collections.Immutable.ImmutableDictionary<Variable, VariableValue>.Empty
            );
    }
}
```

`BaseValues` contains the instance-level base value overrides (variables added at runtime, not from the definition). `ModifierStacks` holds all active modifiers keyed first by variable, then by `ModifierId`. `EffectiveValues` caches the combined result for each variable that has at least one modifier. The definition defaults are not stored in this record — they are always read from the definition directly, which avoids duplicating definition data in the snapshot.

This record lives in the `Scaffold.Entities.States` namespace inside the `com.scaffold.entities.states` package. Note that `ImmutableDictionary` requires `System.Collections.Immutable` — verify that the target Unity version includes this assembly or add the NuGet package. If immutable collections are not available, use regular dictionaries and enforce immutability by convention (only Mutators write to them, and they always produce new instances).

**Step 3.3 — Create `StoreVariableStorage`.** Create `Runtime/StoreVariableStorage.cs`. This class implements `IEntityVariableStorage` and delegates all reads to the Store:

```
using System;
using System.Collections.Generic;
using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class StoreVariableStorage : IEntityVariableStorage
    {
        private readonly Store store;
        private readonly InstanceId instanceId;
        private readonly IEntityDefinition definition;

        public StoreVariableStorage(Store store, InstanceId instanceId, IEntityDefinition definition)
        {
            this.store = store;
            this.instanceId = instanceId;
            this.definition = definition;
        }

        public bool TryGetEffective(Variable key, out VariableValue value)
        {
            var state = store.Get<EntityVariableState>(instanceId);
            if (state.EffectiveValues.TryGetValue(key, out value)) return true;
            if (state.BaseValues.TryGetValue(key, out value)) return true;
            return definition.TryGetDefaultValue(key, out value);
        }

        public bool TryGetBase(Variable key, out VariableValue value)
        {
            var state = store.Get<EntityVariableState>(instanceId);
            if (state.BaseValues.TryGetValue(key, out value)) return true;
            return definition.TryGetDefaultValue(key, out value);
        }

        public IEnumerable<Variable> Variables
        {
            get
            {
                var state = store.Get<EntityVariableState>(instanceId);
                // Union: definition variables + runtime-added variables
                foreach (var key in definition.DefinedVariables) yield return key;
                foreach (var key in state.BaseValues.Keys)
                    if (!definition.TryGetDefaultValue(key, out _)) yield return key;
            }
        }

        public IDisposable Subscribe(Variable key, Action<VariableValue> callback)
        {
            // Fires immediately with current value, then on every state change for this entity.
            if (TryGetEffective(key, out var current)) callback(current);
            return store.Subscribe<EntityVariableState>(instanceId, (_, state, _) =>
            {
                if (state.EffectiveValues.TryGetValue(key, out var v)
                    || state.BaseValues.TryGetValue(key, out v)
                    || definition.TryGetDefaultValue(key, out v))
                    callback(v);
            });
        }

        public void Unsubscribe(Variable key, Action<VariableValue> callback)
        {
            // Store subscriptions are managed via the IDisposable returned by Subscribe.
            // Callers should use the disposable; this method is a no-op for Store-backed storage.
        }

        public IDisposable SubscribeToVariableAdded(Action<Variable, VariableValue> onAdded)
        {
            return store.Subscribe<EntityVariableState>(instanceId, (_, state, ev) =>
            {
                if (ev == StateChangeEvent.Updated)
                {
                    foreach (var kvp in state.BaseValues)
                        onAdded(kvp.Key, kvp.Value);
                }
            });
        }

        public IDisposable SubscribeToVariableRemoved(Action<Variable> onRemoved)
        {
            return store.Subscribe<EntityVariableState>(instanceId, (_, state, ev) =>
            {
                if (ev == StateChangeEvent.Removed)
                    foreach (var key in state.BaseValues.Keys)
                        onRemoved(key);
            });
        }
    }
}
```

Note that `InstanceId` must implement `IReference` for `store.Get<EntityVariableState>(instanceId)` to work. This is a one-line change in `com.scaffold.entities`: add `: IReference` to the `InstanceId` record declaration. `IReference` is an empty marker interface in `com.scaffold.states`. This requires adding a reference from `com.scaffold.entities` to `com.scaffold.states` in the asmdef — if that is unacceptable, define `IReference` in a shared `com.scaffold.records` package that both can reference. Record this decision in the Decision Log when resolved.

**Step 3.4 — Create `StateEntity`.** Create `Runtime/StateEntity.cs`. This class extends `BaseEntityInstance<TDefinition>` and does not implement `IMutableEntity`:

```
using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class StateEntity<TDefinition> : BaseEntityInstance<TDefinition>
        where TDefinition : IEntityDefinition
    {
        public InstanceId InstanceId { get; private set; }

        internal void Initialize(InstanceId id, TDefinition definition, StoreVariableStorage storage)
        {
            InstanceId = id;
            base.Initialize(id, definition, storage);
        }
    }
}
```

`StateEntity` exposes only `InstanceId` as its write-adjacent API surface. Consumers that need to mutate state do so by dispatching to the Store:

```
store.Execute(new AddModifierPayload(entity.InstanceId, variable, modifier, ModifierId.New()));
```

There is no `AddModifier` method on `StateEntity` itself. This is intentional and enforced at compile time — `StateEntity` does not implement `IMutableEntity`.

**Step 3.5 — Create payload types.** Create `Runtime/Payloads.cs` with four payload records. Payloads are plain C# records used to dispatch to the Store:

```
namespace Scaffold.Entities.States
{
    public record AddModifierPayload(
        Scaffold.Entities.InstanceId EntityId,
        Scaffold.Entities.Variable Variable,
        Scaffold.Entities.EntityModifierEntry Entry,
        Scaffold.Entities.ModifierId ModifierId);

    public record RemoveModifierPayload(
        Scaffold.Entities.InstanceId EntityId,
        Scaffold.Entities.Variable Variable,
        Scaffold.Entities.ModifierId ModifierId);

    public record SetVariablePayload(
        Scaffold.Entities.InstanceId EntityId,
        Scaffold.Entities.Variable Variable,
        Scaffold.Entities.VariableValue Value);

    public record AddEntityVariablePayload(
        Scaffold.Entities.InstanceId EntityId,
        Scaffold.Entities.Variable Variable,
        Scaffold.Entities.VariableValue InitialValue);
}
```

For `AddModifierPayload`, the caller generates the `ModifierId` before dispatching:

```
var modId = ModifierId.New();
store.Execute(new AddModifierPayload(entity.InstanceId, hpVariable, entry, modId));
// modId can be stored for later RemoveModifierPayload
```

**Step 3.6 — Create Mutators.** Create `Runtime/EntityVariableMutators.cs`. Each mutator reads the current `EntityVariableState`, applies one operation, recomputes affected effective values using `EntityVariableComputer.ComputeEffective`, and returns a new record via `with`. The definition is not stored in the state record; when computing the effective value, the base value is the value from `BaseValues` if present, otherwise it must come from the definition. Mutators need a way to read definition defaults — pass the definition as a constructor argument to each mutator and register one mutator instance per entity.

```
AddEntityVariableMutator(IEntityDefinition definition)
    : Mutator<EntityVariableState, AddEntityVariablePayload>
AddModifierMutator(IEntityDefinition definition)
    : Mutator<EntityVariableState, AddModifierPayload>
RemoveModifierMutator(IEntityDefinition definition)
    : Mutator<EntityVariableState, RemoveModifierPayload>
SetVariableMutator(IEntityDefinition definition)
    : Mutator<EntityVariableState, SetVariablePayload>
```

Each mutator's `Change` method follows this pattern for `AddModifierMutator`:

```
public override EntityVariableState Change(
    EntityVariableState state, AddModifierPayload payload, IStateScope scope)
{
    var newStack = state.ModifierStacks
        .SetItem(payload.Variable,
            state.ModifierStacks.GetValueOrDefault(payload.Variable,
                ImmutableDictionary<ModifierId, EntityModifierEntry>.Empty)
                .SetItem(payload.ModifierId, payload.Entry));

    VariableValue baseValue = state.BaseValues.TryGetValue(payload.Variable, out var bv)
        ? bv
        : definition.TryGetDefaultValue(payload.Variable, out var dv) ? dv : null;

    var contributions = newStack[payload.Variable].Values
        .Select(e => e.ModifierValue)
        .ToList();
    var effective = EntityVariableComputer.ComputeEffective(baseValue, contributions);

    var newEffective = effective != null
        ? state.EffectiveValues.SetItem(payload.Variable, effective)
        : state.EffectiveValues.Remove(payload.Variable);

    return state with { ModifierStacks = newStack, EffectiveValues = newEffective };
}
```

`RemoveModifierMutator` removes the entry from the inner dictionary; if the inner dictionary becomes empty, removes the key from `ModifierStacks`. It then recomputes the effective value or removes it from `EffectiveValues` if no modifiers remain.

**Step 3.7 — Create `EntityStateFactory`.** Create `Runtime/EntityStateFactory.cs`. The factory creates and registers a `StateEntity`, seeds its `EntityVariableState` slice from definition defaults, and registers the Mutators:

```
public static class EntityStateFactory
{
    public static StateEntity<TDefinition> Create<TDefinition>(
        TDefinition definition,
        Store store,
        InstanceId instanceId)
        where TDefinition : IEntityDefinition
    {
        var initialState = BuildInitialState(definition);
        store.RegisterSlice(instanceId, initialState);
        RegisterMutators(store, definition);

        var storage = new StoreVariableStorage(store, instanceId, definition);
        var entity = new StateEntity<TDefinition>();
        entity.Initialize(instanceId, definition, storage);
        return entity;
    }

    private static EntityVariableState BuildInitialState(IEntityDefinition definition)
    {
        var baseValues = ImmutableDictionary<Variable, VariableValue>.Empty;
        // Definition variables are not stored as base values — they are read directly
        // from the definition. The state record starts empty.
        return EntityVariableState.Empty();
    }

    private static void RegisterMutators(Store store, IEntityDefinition definition)
    {
        store.RegisterMutator(new AddModifierMutator(definition));
        store.RegisterMutator(new RemoveModifierMutator(definition));
        store.RegisterMutator(new SetVariableMutator(definition));
        store.RegisterMutator(new AddEntityVariableMutator(definition));
    }
}
```

**Step 3.8 — Write integration tests.** Create `Tests/StateEntityIntegrationTests.cs`. The most important test is the snapshot round-trip:

```
[Test]
public void Snapshot_RestoresEntityVariableValues_AfterModifierApplied()
{
    // Arrange: definition with HP = 10f
    var definition = new EntityDefinition();
    definition.AddVariable(hpVariable, new FloatVariableValue { Value = 10f });

    var store = new StoreBuilder().Build();
    var instanceId = new InstanceId(1);
    var entity = EntityStateFactory.Create(definition, store, instanceId);

    // Act: save snapshot, apply modifier, restore snapshot
    var snapshot = store.SaveSnapshot();
    var modId = ModifierId.New();
    store.Execute(new AddModifierPayload(instanceId, hpVariable, modifierEntry, modId));

    Assert.That(entity.GetValue<float>(hpVariable), Is.EqualTo(15f)); // 10 + 5

    store.LoadSnapshot(snapshot);

    // Assert: modifier is gone, value is back to 10
    Assert.That(entity.GetValue<float>(hpVariable), Is.EqualTo(10f));
}
```

Additional tests: applying two modifiers and removing one by `ModifierId` returns the correct intermediate value; `StateEntity` does not compile when assigned to `IMutableEntity<T>` (enforce this with a compile-time test comment); the `StoreVariableStorage.Subscribe` fires immediately with the current value on subscription.

Run `validate-changes.cmd` from the repository root. All tests in `Scaffold.Entities.States.Tests` must pass.

## Concrete Steps

All commands run from `c:\Unity\Scaffold` unless stated otherwise.

Validation command — run after completing every milestone before committing:

```
.agents\scripts\validate-changes.cmd
```

EditMode tests — run before the final commit of each milestone:

```
pwsh -NoProfile -File ".agents\scripts\run-editmode-tests.ps1" -TestPlatform EditMode
```

When creating new files under `Assets/`, use Unity MCP to ensure `.meta` files are generated with valid GUIDs. If Unity MCP is unavailable, copy the `.meta` file shape from a sibling file and replace the `guid` value with a fresh 32-character hex string generated by `[System.Guid]::NewGuid().ToString("N")`.

After Milestone 1: confirm `EntityDefinitionAsset` appears in the Unity `Create` asset menu under `Scaffold/Entity/Definition`.

After Milestone 2: confirm existing `EntityInstanceTests` in `Scaffold.Entities.Tests` still pass. Update any tests that call `RemoveModifier(EntityModifierEntry)` to use the new `RemoveModifier(Variable, ModifierId)` signature.

After Milestone 3: run the integration test described in Step 3.8 and confirm it passes.

## Validation and Acceptance

The change is complete when all of the following hold:

1. `validate-changes.cmd` reports `TOTAL:0` from the repository root.
2. All tests in `Scaffold.Entities.Tests` pass.
3. All tests in `Scaffold.Entities.States.Tests` pass, including the snapshot round-trip test.
4. A test calling `entity.GetValue<float>(hpVariable)` on a `StateEntity` returns the same value as calling it on an `EntityInstance` initialized with the same definition and the same modifier applied.
5. No file in `com.scaffold.entities` imports `Scaffold.States` — the packages remain independent. Verify with a grep over the entities `Runtime/` folder: `grep -r "Scaffold.States" Assets/Packages/com.scaffold.entities/Runtime/` should return no results.
6. The `StateEntity<TDefinition>` type does not implement `IMutableEntity<TDefinition>` — verify by confirming that a variable of type `IMutableEntity<EntityDefinition>` cannot be assigned a `StateEntity<EntityDefinition>` value (this will be a compile error if the implementation is correct).

## Idempotence and Recovery

Each milestone ends in a compilable, test-passing state. If work is interrupted mid-milestone, restore to the last clean commit and re-apply only the steps that were not completed. No step destructively modifies Unity asset GUIDs — file renames via Unity MCP preserve `.meta` files. If a rename was partially applied (new file exists but old file also exists), delete the old file manually and verify that `.meta` files are consistent.

If `ImmutableDictionary` is not available in the target Unity version, the plan can be implemented using regular `Dictionary` with defensive copying in each Mutator (`new Dictionary<>(existing)` before mutating). Record this substitution in the Decision Log.

## Artifacts and Notes

Expected output from `validate-changes.cmd` after each milestone:

```
Change Validation Summary
----------------------------
Scripts asmdef audit: PASS (TOTAL:0)
Compilation: PASS (exit code 0)
Analyzers: PASS (TOTAL:0, BLOCKERS:0)
```

If the analyzer reports `SCA3002` (one type per file), split `Payloads.cs` into individual files: `AddModifierPayload.cs`, `RemoveModifierPayload.cs`, `SetVariablePayload.cs`, `AddEntityVariablePayload.cs`.

## Interfaces and Dependencies

Final public signatures that must exist at the end of this plan.

In `Assets/Packages/com.scaffold.entities/Runtime/Core/IEntityDefinition.cs`:

```
public interface IEntityDefinition
{
    bool TryGetDefaultValue(Variable key, out VariableValue value);
    IEnumerable<Variable> DefinedVariables { get; }
}
```

In `Assets/Packages/com.scaffold.entities/Runtime/Core/IEntityVariableStorage.cs`:

```
public interface IEntityVariableStorage
{
    bool TryGetEffective(Variable key, out VariableValue value);
    bool TryGetBase(Variable key, out VariableValue value);
    IEnumerable<Variable> Variables { get; }
    IDisposable Subscribe(Variable key, Action<VariableValue> callback);
    void Unsubscribe(Variable key, Action<VariableValue> callback);
    IDisposable SubscribeToVariableAdded(Action<Variable, VariableValue> onAdded);
    IDisposable SubscribeToVariableRemoved(Action<Variable> onRemoved);
}
```

In `Assets/Packages/com.scaffold.entities/Runtime/Core/IReadOnlyEntity.cs` (updated constraint only):

```
public interface IReadOnlyEntity<out TDefinition> where TDefinition : IEntityDefinition
{
    InstanceId Id { get; }
    T GetValue<T>(Variable key);
    bool TryGetValue<T>(Variable key, out T value);
    TVar GetVariable<TVar>(Variable key) where TVar : VariableValue;
    bool TryGetVariable<TVar>(Variable key, out TVar value) where TVar : VariableValue;
    IDisposable Subscribe(Variable key, Action<VariableValue> onChange);
    void Unsubscribe(Variable key, Action<VariableValue> onChange);
    IDisposable SubscribeToVariableAdded(Action<Variable, VariableValue> onAdded);
    IDisposable SubscribeToVariableRemoved(Action<Variable> onRemoved);
}
```

In `Assets/Packages/com.scaffold.entities/Runtime/Core/IMutableEntity.cs` (new file):

```
public interface IMutableEntity<TDefinition> : IReadOnlyEntity<TDefinition>
    where TDefinition : IEntityDefinition
{
    bool AddVariable(Variable key, VariableValue initialBase);
    bool RemoveVariable(Variable key);
    ModifierId AddModifier(EntityModifierEntry entry);
    bool RemoveModifier(Variable key, ModifierId id);
    void ClearModifiers();
}
```

In `Assets/Packages/com.scaffold.entities/Runtime/Core/ModifierId.cs`:

```
public record ModifierId(System.Guid Id)
{
    public static ModifierId New() => new ModifierId(System.Guid.NewGuid());
}
```

In `Assets/Packages/com.scaffold.entities/Runtime/Core/EntityVariableComputer.cs`:

```
public static class EntityVariableComputer
{
    public static VariableValue ComputeEffective(
        VariableValue baseValue,
        System.Collections.Generic.IReadOnlyList<VariableValue> contributions);
}
```

In `Assets/Packages/com.scaffold.entities/Runtime/Core/BaseEntityInstance.cs`:

```
public abstract class BaseEntityInstance<TDefinition> : IReadOnlyEntity<TDefinition>
    where TDefinition : IEntityDefinition
{
    public InstanceId Id { get; private set; }
    protected TDefinition Definition { get; private set; }
    protected IEntityVariableStorage Storage { get; private set; }
    protected virtual void Initialize(InstanceId id, TDefinition definition, IEntityVariableStorage storage);
    // Full IReadOnlyEntity implementation delegating to Storage
}
```

In `Assets/Packages/com.scaffold.entities/Runtime/Core/EntityInstance.cs` (updated):

```
public class EntityInstance<TDefinition> : BaseEntityInstance<TDefinition>, IMutableEntity<TDefinition>
    where TDefinition : IEntityDefinition
{
    public void Initialize(InstanceId instanceId, TDefinition entityDefinition);
    public bool AddVariable(Variable key, VariableValue initialBase);
    public bool RemoveVariable(Variable key);
    public ModifierId AddModifier(EntityModifierEntry entry);
    public bool RemoveModifier(Variable key, ModifierId id);
    public void ClearModifiers();
}
```

In `Assets/Packages/com.scaffold.entities.states/Runtime/StateEntity.cs`:

```
public sealed class StateEntity<TDefinition> : BaseEntityInstance<TDefinition>
    where TDefinition : IEntityDefinition
{
    public InstanceId InstanceId { get; private set; }
    // Does NOT implement IMutableEntity — no write methods
}
```

In `Assets/Packages/com.scaffold.entities.states/Runtime/EntityStateFactory.cs`:

```
public static class EntityStateFactory
{
    public static StateEntity<TDefinition> Create<TDefinition>(
        TDefinition definition,
        Scaffold.States.Store store,
        InstanceId instanceId)
        where TDefinition : IEntityDefinition;
}
```

## Revision history

- **2026-04-27** — Initial ExecPlan authored from design session covering state/entities incompatibility, Unity decoupling requirements, and bridge package architecture.
- **2026-04-27** — Step 1.1 expanded to cover `VariableEntry` key change from `VariableSO` to `Variable` record. Step 1.4 (`EntityDefinitionAsset`) updated to use a pure `Variable`-keyed bag with no SO references in serialized data. Property drawer approach documented: drawer resolves `VariableSO` by name match at draw time, stores `Variable` record on assignment. Decision Log entry added. Rationale: SOs are authoring/utility helpers and must not appear as first-party keys in base classes or serialized fields.
- **2026-04-27** — Plan amended: **no `AddVariable(VariableSO, ...)` on definitions**. Only `AddVariable(Variable, ...)`; SO→`Variable` cast at call sites. ExecPlan sample code and Step 1.3/1.4 text updated accordingly; Decision Log records the decision.
- **2026-04-27** — **Progress / Surprises / Decision Log** updated for Milestone 1 editor follow-on: `Variable` as serializable `**class`**; `**variableAuthoring**`; `**SerializeReference**` payload rebase; SerializedProperty lifecycle after `**ApplyModifiedProperties**`; removal of AssetDatabase-only SO resolution for `**ResolveSoForDisplay**`. Detailed step text in §Context / §Step 1.1 retains older wording in places — treat **Progress** + **Decision Log** + **Surprises** as the current source of truth for those behaviors until the long-form sections are fully reconciled.

