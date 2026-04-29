# com.scaffold.entities.states

Bridge package: **state-backed** entity variables live in **`Scaffold.States.Store`** as canonical **`EntityVariableState`** (base overrides and per-variable modifier stacks). A **`StateEntity<TDefinition>`** instance is a real **`BaseEntityInstance<TDefinition>`** with **`StoreVariableStorage`**: it implements **`IReadOnlyEntity<TDefinition>`** and **`IMutableEntity<TDefinition>`** so it substitutes for **`EntityInstance<TDefinition>`** in polymorphic APIs.

## Contents

- **`EntityVariableState`** — canonical slice; methods **`WithModifier`**, **`WithoutModifier`**, **`WithBaseValue`**, **`WithVariable`**, **`WithoutVariable`**, **`ResolveEffectiveValues`** return new records.
- **`StateEntity<TDefinition>`** — **`IMutableEntity<TDefinition>`**; mutations call **`store.Execute`** / **`store.ExecuteBatch`** on the same payloads external code would use.
- **`StoreVariableStorage`** (internal) — **`IEntityVariableStorage`** implementation; subscribes to **`EntityVariableState`** for this **`InstanceId`**, caches effective values per rebuild, and fans out **per-variable** subscriptions (value notifications only when the effective value changes).
- Payloads: **`AddModifierPayload`**, **`RemoveModifierPayload`**, **`SetBaseValuePayload`**, **`AddEntityVariablePayload`**, **`RemoveEntityVariablePayload`**.
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

1. **`EntityBridgeContext.RegisterMutators(store)`** once.
2. **`EntityStateFactory.Create`** per **`InstanceId`** (registers the empty **`EntityVariableState`** slice).

## Snapshot semantics

**`EntityVariableState`** slices round-trip. **`StateEntity<TDefinition>`** instances are **not** in snapshots: keep them as **`(store, id, definition)`** handles. **`LoadSnapshot`** restores slices referenced by **`InstanceIds`** verbatim; entity references created for those ids continue to work.

Pruned entities (ids not present in the snapshot and removed from the store) yield **`KeyNotFoundException`** on **`Store.Get<EntityVariableState>(id)`** and therefore on reads through a stale **`StateEntity`** handle.

## Disposal

**`StateEntity<TDefinition>.Dispose()`** disposes **`StoreVariableStorage`**, which **unsubscribes** the **`EntityVariableState`** listener on the store. Call **`Dispose`** when discarding short-lived entities so handlers are not retained.

---

Authoritative module pointer: [`Docs/Core/EntitiesStates.md`](../../../Docs/Core/EntitiesStates.md).
