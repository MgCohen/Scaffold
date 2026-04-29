# com.scaffold.entities.states

Bridge package: **state-backed** entity variables live in **`Scaffold.States.Store`** as canonical **`EntityVariableState`** (base overrides and per-variable modifier stacks). A **`StateEntity<TDefinition>`** instance is a real **`BaseEntityInstance<TDefinition>`** with **`StoreVariableStorage`**: it implements **`IReadOnlyEntity<TDefinition>`** and **`IMutableEntity<TDefinition>`** so it substitutes for **`EntityInstance<TDefinition>`** in polymorphic APIs.

## Contents

- **`EntityVariableState`** — canonical slice; methods **`WithModifier`**, **`WithoutModifier`**, **`WithBaseValue`**, **`WithVariable`**, **`WithoutVariable`**, **`ResolveEffectiveValues`** return new records.
- **`StateEntity<TDefinition>`** — **`IMutableEntity<TDefinition>`**; mutations call **`store.Execute`** / **`store.ExecuteBatch`** on the same payloads external code would use.
- **`StoreVariableStorage`** (internal) — **`IEntityVariableStorage`** implementation; subscribes to **`EntityVariableState`** for this **`InstanceId`**, caches effective values per rebuild, and fans out **per-variable** subscriptions (value notifications only when the effective value changes).
- Payloads: **`AddModifierPayload`** (optional **`ModifierSource`** for attribution), **`RemoveModifierPayload`**, **`RemoveModifiersBySourcePayload`**, **`SetBaseValuePayload`**, **`AddEntityVariablePayload`**, **`RemoveEntityVariablePayload`**.
- **`StateEntityOps.RemoveModifiersFromSource(Store, ModifierSource)`** — atomic cross-entity sweep; builds a single **`ExecuteBatch`** of per-entity **`RemoveModifiersBySourcePayload`** entries (requires **`Store.EnumerateAll<EntityVariableState>()`**).
- **`StateEntity.OnEntityRemoved`** — fires when this entity’s canonical **`EntityVariableState`** slice is removed (unregister or snapshot prune).
- **`EntityBridgeContext.RegisterMutators(Store)`** — register **once** per store; duplicate registration throws **`DuplicateMutatorRegistrationException`**.
- **`EntityStateFactory.Create`** — registers empty **`EntityVariableState`**, constructs **`StoreVariableStorage`** and **`StateEntity<TDefinition>`**. Hold the returned **`StateEntity`** reference for **`GetVariable`** / **`Subscribe`**; it is **not** a **`State`** type and does not appear in **`SaveSnapshot()`** output.

## Quickstart

```csharp
var store = new StoreBuilder().Build();
EntityBridgeContext.RegisterMutators(store);
var id = new InstanceId(1);
var entity = EntityStateFactory.Create(definition, store, id);

// Reads: IReadOnlyEntity<TDefinition>
float hp = entity.GetVariable<float>(healthVariable);

// Writes: IMutableEntity<TDefinition> OR store.Execute / ExecuteBatch
entity.AddModifier(new EntityModifierEntry(healthVariable, new FloatAddModifier(5f)));
store.Execute(id, new AddModifierPayload(id, healthVariable, new FloatAddModifier(2f), ModifierId.New()));

// Snapshot: only EntityVariableState is serialized
var snapshot = store.SaveSnapshot();
store.LoadSnapshot(snapshot);
// Same entity reference continues to read the restored slice for this id.
```

## Atomic multi-payload commit (P0.3)

Use **`Store.ExecuteBatch(IReadOnlyList<object> payloads)`** for all-or-nothing commits. All payloads share one **`MutatorRunner`** overlay; if any mutator throws, the overlay is discarded and **no partial state** is committed.

**Batch coalescing:** One **`ExecuteBatch`** commits once per affected slice. Subscribers see **one `Updated`** per **`(reference, state type)`** pair for that commit, with no intermediate states from other payloads in the same batch.

## Modifier source attribution (P0.2)

**`ModifierSource`** (in **`Scaffold.Entities`**) tags modifiers when applying **`AddModifierPayload`** (optional fifth argument). Clear all modifiers from a given source on one entity with **`RemoveModifiersBySourcePayload`** and **`store.Execute`**, or clear across every **`EntityVariableState`** slice atomically with **`StateEntityOps.RemoveModifiersFromSource(store, source)`** (one **`ExecuteBatch`**). Use payloads for attribution; **`IMutableEntity.AddModifier`** does not take a source.

## Definition swap (G2)

**`StateEntity`** carries **`Definition`** on the handle, not in **`EntityVariableState`**. To “transform” an entity (e.g. polymorph), create a **`StateEntity<TNewDef>`** for the **same `InstanceId`** with the new definition. The new handle resolves defaults from the new definition; an old handle still uses the old definition if retained. There is no state-level **`SetDefinitionPayload`**.

## Subscription patterns

| Pattern | API |
|--------|-----|
| **Per variable on one entity** | **`entity.Subscribe(variable, onChange)`** — fires immediately with the current value, then when that variable’s **effective** value changes. Unchanged effective values after an unrelated mutation do not notify. |
| **One entity’s canonical slice** | **`store.Subscribe<EntityVariableState>(id, (ref, state, evt) => …)`** |
| **All entities with this slice type** | **`store.SubscribeAllReferences<EntityVariableState>((ref, state, evt) => …)`** — filter by **`ref`** on the receiving side. |

**Structural changes** on **`entity.SubscribeToVariableStructuralChanges`**: **`Added`** / **`Removed`** are inferred by **diffing** the union of **`BaseValues`**, **`ModifierStacks`**, and **`definition.DefinedVariables`** across rebuilds, matching **`LocalVariableStorage`** / **`VariableBag`** (**`Removed`** passes **`null`** as the value). A single **`ExecuteBatch`** that removes and re-adds the same variable can produce **no net structural event**. For per-mutation granularity, subscribe to **`EntityVariableState`** and diff yourself.

## Effective-value caching

After each canonical change, **`StoreVariableStorage`** rebuilds an internal effective-value map once per subscribed rebuild. **`IReadOnlyEntity`** reads use that cache.

## Modifier ordering

Stacks are per-**`Variable`**, ordered by **`Modifier.Order`** ascending with **insertion-order** tiebreak. Layer **order ranges** are recommended for continuous-effect styles.

## Bootstrap

1. **`EntityBridgeContext.RegisterMutators(store)`** once per **`Store`** (a second call throws **`DuplicateMutatorRegistrationException`** — P2.8).
2. **`EntityStateFactory.Create`** per **`InstanceId`** (registers the empty **`EntityVariableState`** slice).

## Snapshot semantics

**`EntityVariableState`** slices round-trip. **`StateEntity<TDefinition>`** instances are **not** in snapshots: keep them as **`(store, id, definition)`** handles. **`LoadSnapshot`** restores slices referenced by **`InstanceIds`** verbatim; entity references created for those ids continue to work.

**Re-register after unregister:** If a slice was removed from the store but appears in the snapshot being loaded (e.g. entity destroyed, then rolling back to an older snapshot that still contains it), **`LoadSnapshot`** re-creates that canonical row. A kept **`StateEntity`** handle for that id can read again after load.

Pruned entities (present in the store but **not** in the snapshot) are removed; reads through a stale **`StateEntity`** handle **`KeyNotFoundException`**, and **`OnEntityRemoved`** runs if subscribed.

## Disposal

**`StateEntity<TDefinition>.Dispose()`** disposes **`StoreVariableStorage`**, which **unsubscribes** the **`EntityVariableState`** listener on the store. Call **`Dispose`** when discarding short-lived entities so handlers are not retained.

## `Variables` enumeration order

**`IEntityVariableStorage.Variables`** on **`StoreVariableStorage`** is ordered by **`Variable.Key`** using **ordinal** string comparison (deterministic for replay/UI).

---

Authoritative module pointer: [`Docs/Core/EntitiesStates.md`](../../../Docs/Core/EntitiesStates.md).
