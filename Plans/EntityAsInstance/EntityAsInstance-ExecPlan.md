# Entity as instance: state-backed entities are EntityInstances, not aggregate records

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. Repository policy for ExecPlans is defined in `PLANS.md` at the repository root; this document is maintained in accordance with that file.

This plan supersedes `Plans/EntityAsAggregate/EntityAsAggregate-ExecPlan.md`. That earlier plan reshaped `StateEntity<TDef>` from a class extending `BaseEntityInstance<TDef>` into an `AggregateState` record produced by `EntityStateProvider<TDef>`. The reshape worked — the bridge package on `feature/entities-state-bridge` ships that design today and all 22 integration tests pass — but external review by the first prospective consumer (Card-Framework) flagged that the record is a *parallel* read shape rather than a polymorphic implementation: their engine code is written against `IReadOnlyEntity<TDefinition>` and cannot accept a `StateEntity<TDef>` record. Exposing both pure `EntityInstance<TDef>` and a state-backed entity through a single `IReadOnlyEntity<TDef>` reference is the original goal of the entire `com.scaffold.entities.states` bridge — that goal slipped out of view during the aggregate-record detour. This plan reverses the detour: state-backed entities become a concrete `BaseEntityInstance<TDef>` subclass again, with a state-backed `IEntityVariableStorage` implementation that translates store-level events into the per-variable callback shape that `IReadOnlyEntity<TDef>` requires.


## Purpose / Big Picture

Today, after `Plans/EntityAsAggregate/EntityAsAggregate-ExecPlan.md` landed, `Assets/Packages/com.scaffold.entities.states/` exposes two types:

- `EntityVariableState : State` — the canonical slice, holding `(BaseValues, ModifierStacks)` per entity, keyed by `InstanceId`.
- `StateEntity<TDef> : AggregateState` — an immutable `record` produced by `EntityStateProvider<TDef> : AggregateProvider<StateEntity<TDef>>` that folds the canonical slice plus the entity's `IEntityDefinition` into `(Id, Definition, BaseValues, ModifierStacks, EffectiveValues)`. Consumers read it via `store.Get<StateEntity<TDef>>(id).GetVariable<T>(key)`.

This shape mirrors the read methods on `IReadOnlyEntity<TDef>` (`GetVariable<T>`, `TryGetVariable<T>`) but does not implement the interface, because `IReadOnlyEntity<TDef>` also has subscription methods (`Subscribe(Variable, Action<VariableValue>)`, `SubscribeToVariableStructuralChanges(...)`) that an immutable record cannot meaningfully serve — the record is a frozen-in-time snapshot, not a live handle. As a result, code that wants to accept "any entity" (whether pure-memory `EntityInstance<TDef>` or store-backed) must branch on the backing rather than holding a single `IReadOnlyEntity<TDef>` reference. Card-Framework's review made this concrete: their `IGameStateProvider`, query, and validation layers all consume `IReadOnlyEntity<TDef>` polymorphically and have no clean way to accept a `StateEntity<TDef>` record.

After this plan, state-backed entities live inside the same hierarchy as pure-memory ones:

- `BaseEntityInstance<TDef> : IReadOnlyEntity<TDef>` (unchanged, lives in `com.scaffold.entities`) does the read work by delegating to an `IEntityVariableStorage`.
- `LocalVariableStorage : IEntityVariableStorage` (unchanged, lives in `com.scaffold.entities`) is the in-memory implementation used by the existing standalone `EntityInstance<TDef>`.
- `StoreVariableStorage : IEntityVariableStorage` (new, in this bridge package) reads variable values from an `EntityVariableState` slice on a `Store`, caches effective values, and translates whole-aggregate change notifications into per-variable callbacks for `Subscribe(Variable, ...)` consumers.
- `StateEntity<TDef> : BaseEntityInstance<TDef>, IMutableEntity<TDef>` (new shape — same name, different role) is a concrete entity initialized with a `StoreVariableStorage`. Its mutation methods (`AddModifier`, `RemoveModifier`, `AddVariable`, `RemoveVariable`, `ClearModifiers`) translate to `store.Execute(...)` calls. Both pure and state-backed entities are now substitutable via `IReadOnlyEntity<TDef>` (read-only) or `IMutableEntity<TDef>` (mutating).

The existing `StateEntity<TDef>` record and `EntityStateProvider<TDef>` aggregate provider are removed. Effective-value caching, which was the aggregate's only real job, moves into `StoreVariableStorage` itself: the storage subscribes to `EntityVariableState` changes for its entity, recomputes effective values once per change, and demuxes per-variable callbacks. This collapses the bridge to a single state type (`EntityVariableState`) and a single consumer-facing read shape (`IReadOnlyEntity<TDef>`), eliminating the parallel-hierarchy problem at its root.

The plan additionally bundles one of the P0 items from Card-Framework's review:

- **P0.1 — Variable removal.** Today `EntityVariableState.WithVariable` is a no-op when the key already exists, and there is no payload or method to remove a variable. The state-backed `IMutableEntity.RemoveVariable(Variable)` path requires a `RemoveEntityVariablePayload` and a corresponding mutator. This plan ships them.

P0.2 (modifier source attribution) and P0.3 (atomic multi-payload commit) are out of scope. P0.3 is already satisfied by `Store.ExecuteBatch` and only needs documentation; P0.2 reshapes `ActiveModifier` and `EntityModifierEntry` and deserves its own plan because it touches both core packages.

A novice reader can see this working through three observable changes:

1. `var entity = EntityStateFactory.Create(definition, store, instanceId)` returns a `StateEntity<TDefinition>` value whose runtime type is assignable to `IReadOnlyEntity<TDefinition>` and `IMutableEntity<TDefinition>`. The compile-time test `IReadOnlyEntity<TDef> handle = entity;` succeeds.
2. Calling `entity.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)))` produces the same observable state as `store.Execute(new AddModifierPayload(instanceId, hp, modifier, ModifierId.New()))`. Both paths route through the same mutator and end at the same `EntityVariableState`.
3. `entity.Subscribe(hp, value => Captured.Add(value))` fires once with the current value at subscription time and once for each subsequent `Execute` that changes `hp`'s effective value. A subscriber that only listens to `attack` does not fire when `hp` changes.

Neither core package gains new dependencies. The bridge package's runtime asmdef references stay the same: `Scaffold.Entities` and `Scaffold.States`.


## Progress

- [x] Milestone 1 — `StoreVariableStorage` + new `StateEntity<TDef>` class. Replace the aggregate-record approach with an `IEntityVariableStorage` implementation that reads from `EntityVariableState`, plus a `BaseEntityInstance<TDef>` subclass that owns a mutation API translating to `store.Execute(...)`. Existing tests are migrated to assert through the entity API. After this milestone, every existing integration test passes against the new shape and the parallel record/provider types are deleted.
- [x] Milestone 2 — Variable removal (P0.1). Add `RemoveEntityVariablePayload`, `RemoveEntityVariableMutator`, an `EntityVariableState.WithoutVariable(Variable)` method that clears the variable's base value and any pending modifier stack atomically, and the `IMutableEntity.RemoveVariable` translation on `StateEntity<TDef>`. Add a regression test asserting that after `RemoveVariable(hp)`, `entity.TryGetVariable<float>(hp, out _)` falls back to the definition default (or returns false if the definition does not declare it).
- [x] Milestone 3 — Documentation pass. Update the package `README.md` (or create one) explaining that `StateEntity<TDef>` is an `IReadOnlyEntity<TDef>` and `IMutableEntity<TDef>`, that `Store.ExecuteBatch` is the supported atomic-multi-payload primitive (P0.3), that effective values are cached on canonical change inside the storage, and the recommended subscription patterns (per-variable on the entity vs. whole-aggregate on the store).


## Surprises & Discoveries

- **Store subscription teardown:** `Store.Subscribe` did not return an `IDisposable`, and `IStateEventHandler` had no unsubscribe API. **`Store.Unsubscribe<TState>(IReference, Action<...>)`**, **`IStateEventHandler.Unsubscribe`**, **`Ledger.RemoveSubscription`**, and **`TypedSubscription.Matches`** were added so `StoreVariableStorage.Dispose()` can detach from `EntityVariableState` notifications (implements decision **3B** without leaking handlers).
- **Effective-value change detection:** `VariableValue` reference types do not implement blanket value equality. `StoreVariableStorage` compares common `IVariableValue<T>` payload types (`float`, `int`, `double`, `long`, `bool`, `string`) by inner value and falls back to reference equality for other shapes.
- **Pruned entity test:** `AggregateSlice_RetainsCachedRecord_AfterPruneOfCanonicalState` assumed a cached aggregate row survived canonical prune. With only `EntityVariableState` in the store, **reads through a stale `StateEntity` handle after prune throw `KeyNotFoundException`** (from `store.Get<EntityVariableState>(id)` inside the storage). The test was replaced accordingly.


## Decision Log

- **Decision:** Reverse `Plans/EntityAsAggregate/EntityAsAggregate-ExecPlan.md`'s reshape of `StateEntity<TDef>` from a `BaseEntityInstance<TDef>` subclass into an `AggregateState` record. Restore the `BaseEntityInstance<TDef>` subclass shape.

  Rationale: external review by Card-Framework, the first prospective consumer, identified that the record-shaped `StateEntity` is a parallel read surface that does not implement `IReadOnlyEntity<TDef>` and therefore breaks polymorphism with the pure-memory `EntityInstance<TDef>`. The original `com.scaffold.entities.states` charter — "consumers hold an `EntityInstance` whose storage is a state store" — is better served by the instance shape.

  Tradeoffs accepted: per-variable subscriptions become slightly more expensive (a per-instance `Dictionary<Variable, List<Action<VariableValue>>>` plus a walk on each rebuild); structural-change events for state-backed entities are inferred by diffing the key set across rebuilds rather than emitted per mutation; consumers must hold the `StateEntity<TDef>` instance reference rather than reading via `store.Get<StateEntity<TDef>>(id)` because `StateEntity<TDef>` is no longer a state type. Lost record affordances (`with` expressions, value-equality on the public surface) are not in use by any current consumer.

  Author: Design session, 2026-04-29.

- **Decision:** Drop the `AggregateSlice`/`EntityStateProvider` plumbing entirely rather than keeping it as an internal effective-value cache.

  Rationale: the aggregate's only job is to cache `EffectiveValues` on canonical change. `StoreVariableStorage` is a natural home for that cache — it already needs to subscribe to `EntityVariableState` changes to fire per-variable callbacks, so caching effective values inline costs one extra dictionary and zero extra subscriptions. Keeping the aggregate would mean two state types, one of which is internal-only, and would complicate the snapshot story (the existing test `Snapshot_DoesNotIncludeAggregateState` exists to enforce a property that is no longer relevant once there is only one state type).

  Author: Design session, 2026-04-29.

- **Decision:** `StoreVariableStorage.Subscribe(variable, callback)` fires `callback(currentValue)` immediately on subscription (matching `LocalVariableStorage` behavior), and on each subsequent rebuild fires `callback(newValue)` *only if the variable's effective value has changed* relative to the previously-observed value.

  Rationale: `LocalVariableStorage` calls `RecalculateAndNotify` which always fires after a recalc — that is acceptable in-process because mutations are scoped per variable. State-backed mutations are aggregate-level: every payload rebuilds the entity as a whole, and a "fire on every rebuild" policy would wake every subscribed callback on every unrelated change. Comparing prev-vs-new effective value per subscribed variable is cheap, mirrors what consumers expect (`onChange` should mean "the value I subscribed to changed"), and matches Card-Framework's described usage in `IAvailableActions` evaluation hot paths.

  Author: Design session, 2026-04-29.

- **Decision:** `StoreVariableStorage.SubscribeToVariableStructuralChanges` emits `Added`/`Removed` events by diffing the key set (`union of EntityVariableState.BaseValues.Keys, EntityVariableState.ModifierStacks.Keys, definition.DefinedVariables`) across rebuilds, not by hooking the mutator path.

  Rationale: hooking the mutator path would require new infrastructure in `Scaffold.States` to emit per-payload structural notifications, and the diff approach is sufficient for all known consumer use cases (token cleanup, tag clearing, transient-state on stack-resolution). The known limitation — a single `ExecuteBatch` containing both a remove and a re-add of the same variable produces no net structural change and emits nothing — is acceptable because consumers who care about intermediate states can subscribe to canonical `EntityVariableState` updates directly. This limitation is recorded in the bridge `README` (Milestone 3).

  Author: Design session, 2026-04-29.

- **Decision:** Bundle P0.1 (variable removal) into this plan; defer P0.2 (modifier source attribution) and P0.3 (atomic multi-payload commit) to separate work.

  Rationale: P0.1 is small (one payload, one mutator, one method on `EntityVariableState`, one method on `StateEntity<TDef>`), aligns with the surface this plan reshapes (`IMutableEntity.RemoveVariable` is part of the contract `StateEntity<TDef>` newly implements), and unblocks Card-Framework's token-cleanup and tag-clearing use cases. P0.2 reshapes `ActiveModifier` and `EntityModifierEntry` in the core entities package and deserves its own plan with cross-package coordination. P0.3 is already satisfied by `Store.ExecuteBatch` and only needs documentation.

  Author: Design session, 2026-04-29.

- **Decision:** Keep the public type name `StateEntity<TDef>` for the new instance class, even though that name was used for the aggregate record being deleted.

  Rationale: no external consumer ships against this branch yet, the Card-Framework review explicitly referred to the consumer-facing type as `StateEntity<TDef>`, and inventing a new name (e.g. `StateBackedEntityInstance<TDef>`, `StoreEntity<TDef>`) for the conceptually-same role would multiply the parallel-hierarchy confusion the plan is trying to eliminate. The change in role (record → class) is large enough that any in-flight code reading `StateEntity` will see compile errors at the change boundary, so silent behavior drift is not a risk.

  Author: Design session, 2026-04-29.

- **Decision:** Use **`InternalsVisibleTo("Scaffold.Entities.States")`** on `Scaffold.Entities` so the bridge can reuse **`EmptyDisposable`** and **`CallbackDisposable`** (choice **1A**, minimal friend assembly **5A**).

  Rationale: avoid duplicating subscription helper types; only the states bridge assembly is granted access.

  Author: Consumer Q&A, 2026-04-29.

- **Decision:** **`IMutableEntity.AddVariable`** on `StateEntity<TDef>` returns **`false`** when **`EntityVariableState.BaseValues` already contains the key**, without calling **`Execute`** (choice **2A**).

  Rationale: align the `bool` contract with `LocalVariableStorage` / duplicate-add rejection while leaving the `AddEntityVariableMutator` no-op behavior unchanged for direct payload use.

  Author: Consumer Q&A, 2026-04-29.

- **Decision:** **`StoreVariableStorage` implements `IDisposable`** and **`StateEntity<TDef>.Dispose()`** disposes the storage so the **`EntityVariableState`** subscription is removed via **`Store.Unsubscribe`** (choice **3B**).

  Rationale: supports short-lived state-backed entities without leaving per-id handlers attached to the store.

  Author: Consumer Q&A, 2026-04-29.

- **Decision:** **`SubscribeToVariableStructuralChanges`** on **`StoreVariableStorage`** matches **`LocalVariableStorage` / `VariableBag`**: **`Added`** carries the current **`VariableValue`**; **`Removed`** uses a **`null`** third argument (choice **4A**).

  Rationale: polymorphic code that relies on `LocalVariableStorage` structural semantics sees the same convention on store-backed entities.

  Author: Consumer Q&A, 2026-04-29.


## Outcomes & Retrospective

Implementation completed 2026-04-29: **`StoreVariableStorage`**, **`StateEntity<TDef>`** as **`BaseEntityInstance` / `IMutableEntity`**, **`WithoutVariable`** + **`RemoveEntityVariablePayload`**, store **`Unsubscribe`** for disposal, package **`README`**, integration tests migrated (29 passing in **`Scaffold.Entities.States.Tests`**). Quality gate: **`validate-changes.ps1 -SkipTests`** reports **TOTAL:0**.


## Context and Orientation

This section explains the relevant parts of the three packages as they exist today on `feature/entities-state-bridge` at commit `da6cf58`. Read it fully before touching any code.

### `com.scaffold.entities` (the core entity package)

Two interfaces and one concrete class are load-bearing for this plan:

`Assets/Packages/com.scaffold.entities/Runtime/Core/Contracts/IReadOnlyEntity.cs` defines `public interface IReadOnlyEntity<out TDefinition> where TDefinition : IEntityDefinition`. Members: `InstanceId Id`, `T GetVariable<T>(Variable key)`, `bool TryGetVariable<T>(Variable key, out T value)`, `IDisposable Subscribe(Variable key, Action<VariableValue> onChange)`, `void Unsubscribe(Variable key, Action<VariableValue> onChange)`, `IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler)`. The `Subscribe` and structural-change methods are the parts an immutable record cannot serve.

`Assets/Packages/com.scaffold.entities/Runtime/Core/Contracts/IMutableEntity.cs` defines `public interface IMutableEntity<TDefinition> : IReadOnlyEntity<TDefinition>` and adds `bool AddVariable(Variable key, VariableValue initialBase)`, `bool RemoveVariable(Variable key)`, `ModifierId AddModifier(EntityModifierEntry entry)`, `bool RemoveModifier(Variable key, ModifierId id)`, `void ClearModifiers()`. State-backed mutation must implement all five.

`Assets/Packages/com.scaffold.entities/Runtime/Core/Instance/BaseEntityInstance.cs` is `public abstract class BaseEntityInstance<TDefinition> : IReadOnlyEntity<TDefinition>`. It holds a `[SerializeField] InstanceId id`, `[SerializeField] protected TDefinition definition`, and an `IEntityVariableStorage Storage` set in `Initialize(InstanceId, TDefinition, IEntityVariableStorage)`. Every read method on the interface delegates to `Storage`. Subclasses bring their own storage and add write methods. The class is `[Serializable]` and uses `[SerializeField]` for inspector authoring of the existing pure `EntityInstance<TDefinition>`; this plan's new subclass does not need Unity inspector authoring (state-backed entities are constructed by `EntityStateFactory`, not authored as components), so the `[SerializeField]` attributes are inert in that path but harmless.

`Assets/Packages/com.scaffold.entities/Runtime/Core/Instance/IEntityVariableStorage.cs` is the storage seam. Members: `bool TryGetEffective(Variable, out VariableValue)`, `bool TryGetBase(Variable, out VariableValue)`, `IEnumerable<Variable> Variables`, `IDisposable Subscribe(Variable, Action<VariableValue>)`, `void Unsubscribe(Variable, Action<VariableValue>)`, `IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?>)`. `LocalVariableStorage` is the existing pure-memory implementation. This plan adds `StoreVariableStorage` as the second implementation.

`Assets/Packages/com.scaffold.entities/Runtime/Core/Instance/EntityInstance.cs` is the existing `public class EntityInstance<TDefinition> : BaseEntityInstance<TDefinition>, IMutableEntity<TDefinition>`. It owns a `LocalVariableStorage` field, wires it in `Initialize`, and delegates all `IMutableEntity` methods to that storage. The new `StateEntity<TDefinition>` parallels this exactly: own a `StoreVariableStorage`, wire it in `Initialize`, delegate to `store.Execute(...)` for mutations.

`Assets/Packages/com.scaffold.entities/Runtime/Core/Variables/Variable.cs` is the canonical key — a serializable class with `Key` (string) and `Type` (string). Equality is by `(Key, Type)`. Used as `IReadOnlyDictionary` key throughout.

`Assets/Packages/com.scaffold.entities/Runtime/Core/Identity/InstanceId.cs` is a serializable class implementing `Scaffold.States.IReference` (added during the original `Plans/StateBackedEntities` work). Equality is by `Id` (int).

`Assets/Packages/com.scaffold.entities/Runtime/Core/Identity/ModifierId.cs` is a `Guid`-backed `readonly struct`. Generated by the caller before dispatching `AddModifierPayload`.

`Assets/Packages/com.scaffold.entities/Runtime/Core/Modifiers/VariableModifier.cs` is the abstract base for modifiers; `Order` is the only public surface. Concrete subclasses (`FloatAddModifier`, `FloatMultiplyModifier`, etc.) implement `Apply(T current)`. `EntityModifierEntry` (in `Runtime/Core/Definitions`) is the `(Variable, VariableModifier)` pair used by `IMutableEntity.AddModifier`.

`Assets/Packages/com.scaffold.entities/Runtime/Core/VariableBags/VariableStructuralChange.cs` is the enum used by `SubscribeToVariableStructuralChanges`: `Added`, `Removed`. (Confirm exact members at implementation time.)

### `com.scaffold.states` (the store package)

`Assets/Packages/com.scaffold.states/Runtime/Store.cs` is the central object. The methods this plan uses:

- `Store.RegisterSlice(IReference, State)` registers a canonical mutable slice. `EntityStateFactory` calls this with `EntityVariableState.Empty` per entity.
- `Store.RegisterMutator(Mutator<TState, TPayload>)` registers a payload-to-state binding. `EntityBridgeContext.RegisterMutators(store)` calls this once per store for the bridge's mutator types.
- `Store.Execute(IReference, payload)` dispatches a payload through registered mutators. Atomic per-call: if any mutator throws, the overlay is discarded via `MutatorRunner.OnReturnedToPool → scratchpad.Reset()`.
- `Store.ExecuteBatch(IReadOnlyList<object> payloads)` runs multiple payloads through a single overlay. All-or-nothing. This is the answer to Card-Framework's P0.3 question — already implemented.
- `Store.Get<TState>(IReference)` returns the current canonical or aggregate state. After this plan, only `EntityVariableState` is registered per entity.
- `Store.Subscribe<TState>(IReference, Action<IReference, TState, StateChangeEvent>)` registers a subscription on a slice. `StoreVariableStorage` uses this to listen for `EntityVariableState` changes on its entity.
- `Store.SaveSnapshot()` / `Store.LoadSnapshot(snapshot)` capture and restore canonical slices. `LoadSnapshot` re-keys slices by the snapshot's `IReference` values verbatim — InstanceIds round-trip unchanged.

`Assets/Packages/com.scaffold.states/Runtime/Mutators/Mutator.cs` defines `Mutator<TState, TPayload>` with `abstract TState Change(TState state, TPayload payload, IStateScope scope)`. The bridge's mutators are one-line translations from payload to `EntityVariableState.With*` method calls.

`Assets/Packages/com.scaffold.states/Runtime/State/State.cs` is the marker base for canonical-slice records. After this plan, the bridge has exactly one: `EntityVariableState`.

`Assets/Packages/com.scaffold.states/Runtime/Pipeline/MutatorRegistry.cs` throws `DuplicateMutatorRegistrationException` if `EntityBridgeContext.RegisterMutators(store)` is called twice on the same store for the same mutator type. The plan keeps that behavior; multiple registrations on the same store are a programmer error.

### `com.scaffold.entities.states` (the bridge package, current state)

The package on `feature/entities-state-bridge` at `da6cf58` contains:

- `Runtime/EntityVariableState.cs` — `public sealed record EntityVariableState(IReadOnlyDictionary<Variable, VariableValue> BaseValues, IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks) : State`. Methods: `WithModifier`, `WithoutModifier`, `WithBaseValue`, `WithVariable` (no-op on existing key), `ResolveEffectiveValues(IEntityDefinition)`. **Kept; gains `WithoutVariable(Variable)` in Milestone 2.**
- `Runtime/StateEntity.cs` — `public sealed record StateEntity<TDefinition>(InstanceId Id, TDefinition Definition, IReadOnlyDictionary<Variable, VariableValue> BaseValues, IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks, IReadOnlyDictionary<Variable, VariableValue> EffectiveValues) : AggregateState`. **Deleted; replaced by a class with the same name in Milestone 1.**
- `Runtime/EntityStateProvider.cs` — `internal sealed class EntityStateProvider<TDefinition> : AggregateProvider<StateEntity<TDefinition>>`. **Deleted in Milestone 1.**
- `Runtime/EntityBridgeContext.cs` — `public static class EntityBridgeContext` with `RegisterMutators(Store)`. **Kept; the registration method gains the `RemoveEntityVariableMutator` in Milestone 2.**
- `Runtime/EntityStateFactory.cs` — `public static StateEntity<TDefinition> Create<TDefinition>(TDefinition, Store, InstanceId)`. **Kept signature; reshaped body in Milestone 1 to construct a `StoreVariableStorage` and a new `StateEntity<TDef>` class.**
- `Runtime/Mutators/AddModifierMutator.cs`, `RemoveModifierMutator.cs`, `SetBaseValueMutator.cs`, `AddEntityVariableMutator.cs` — all `internal sealed class : Mutator<EntityVariableState, *Payload>`. **Kept unchanged; `RemoveEntityVariableMutator` added in Milestone 2.**
- `Runtime/Payloads/AddModifierPayload.cs`, `RemoveModifierPayload.cs`, `SetBaseValuePayload.cs`, `AddEntityVariablePayload.cs` — public sealed records. **Kept unchanged; `RemoveEntityVariablePayload` added in Milestone 2.**
- `Tests/StateEntityIntegrationTests.cs` — 22 tests. **All migrated in Milestone 1 to assert through the entity API; net behavior unchanged.**

The asmdef at `Runtime/Scaffold.Entities.States.asmdef` references `Scaffold.Entities` and `Scaffold.States`. No changes needed.


## Plan of Work

### Milestone 1 — `StoreVariableStorage` + new `StateEntity<TDef>` class

The goal of this milestone is to replace the aggregate-record approach with a `BaseEntityInstance<TDef>`-shaped state-backed entity. After this milestone, `EntityStateFactory.Create` returns a `StateEntity<TDefinition>` that is a `BaseEntityInstance<TDefinition>`, implements `IMutableEntity<TDefinition>`, and supports per-variable subscriptions through `StoreVariableStorage`. All existing integration tests pass against the new shape.

**Step 1.1 — Create `StoreVariableStorage`.**

Create `Assets/Packages/com.scaffold.entities.states/Runtime/StoreVariableStorage.cs`. The class is `internal sealed class StoreVariableStorage : IEntityVariableStorage`. Constructor: `(Store store, InstanceId id, IEntityDefinition definition)`. Three pieces of internal state:

- `Dictionary<Variable, VariableValue> effectiveCache` — recomputed each rebuild, used by reads and as the prev-value reference for change detection.
- `Dictionary<Variable, List<Action<VariableValue>>> perVariableSubscribers` — fan-out for `Subscribe(Variable, ...)`.
- `List<Action<VariableStructuralChange, Variable, VariableValue?>> structuralSubscribers` — fan-out for `SubscribeToVariableStructuralChanges`.
- `HashSet<Variable> lastKeySnapshot` — the union of `EntityVariableState.BaseValues.Keys`, `ModifierStacks.Keys`, and `definition.DefinedVariables` as observed on the previous rebuild. Used to compute `Added`/`Removed` diffs.

In the constructor, immediately compute `effectiveCache` and `lastKeySnapshot` from the current `store.Get<EntityVariableState>(id)` so reads work before any mutation happens. Then subscribe to `store.Subscribe<EntityVariableState>(id, OnStateChanged)` and store the disposable so `Dispose` (if needed) can release it.

Implement read methods:

- `TryGetEffective(Variable key, out VariableValue value)`: read from `effectiveCache` first; if absent, read from current `EntityVariableState.BaseValues`; if absent, fall back to `definition.TryGetDefaultValue(key, out value)`. Return whether any of those produced a value.
- `TryGetBase(Variable key, out VariableValue value)`: skip the cache; read from `EntityVariableState.BaseValues`, then `definition.TryGetDefaultValue`.
- `Variables`: yield each entry of `lastKeySnapshot`. The snapshot is recomputed on every rebuild, so this property reflects the current state.

Implement `Subscribe(Variable key, Action<VariableValue> onChange)`:

- If `key` or `onChange` is null, return `EmptyDisposable.Instance` (matches `LocalVariableStorage`).
- Append `onChange` to `perVariableSubscribers[key]` (creating the list if missing).
- If `TryGetEffective(key, out var current)`, immediately invoke `onChange(current)` (mirrors `LocalVariableStorage.cs:79`).
- Return a `CallbackDisposable` whose dispose action removes `onChange` from the list and removes the list entry if it becomes empty.

Implement `Unsubscribe(Variable key, Action<VariableValue> onChange)`: symmetric to `Subscribe`'s removal path. No-op if the key/handler is not present.

Implement `SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler)`:

- If `handler` is null, return `EmptyDisposable.Instance`.
- Append `handler` to `structuralSubscribers`.
- Return a `CallbackDisposable` that removes it.

Implement `OnStateChanged(IReference, EntityVariableState newState, StateChangeEvent ev)`:

- If `ev` is `StateChangeEvent.Removed`, walk every key in `lastKeySnapshot` and emit `Removed` to all structural subscribers, then clear `effectiveCache` and `lastKeySnapshot`. Return.
- Otherwise (`Created` or `Updated`):
  1. Compute `newEffective = newState.ResolveEffectiveValues(definition)`. This returns only variables that have at least one modifier; combine with `newState.BaseValues` and `definition.DefinedVariables` to form the "current value per known variable" map for change detection. (Reuse the resolution logic; do not duplicate it.)
  2. Compute `newKeys = union(newState.BaseValues.Keys, newState.ModifierStacks.Keys, definition.DefinedVariables)`.
  3. For each `Variable v` in `perVariableSubscribers.Keys`:
     - Compute `currentValue` for `v`: try `newEffective[v]`, then `newState.BaseValues[v]`, then `definition.TryGetDefaultValue(v, out value)`. If still missing, treat as null.
     - Look up `prevValue = effectiveCache.TryGetValue(v, out var p) ? p : null`. (Or take from base + default if not in cache; same fallback chain.)
     - If `currentValue != prevValue` (use `Equals`, not reference equality — `VariableValue` types implement value equality), invoke every subscriber for `v` with `currentValue`. Skip if `currentValue` is null (subscriber received whatever they had; explicit null is "variable went away" and is reported through structural events instead).
  4. For each variable in `newKeys.Except(lastKeySnapshot)`: emit `Added` to each structural subscriber with the current value.
  5. For each variable in `lastKeySnapshot.Except(newKeys)`: emit `Removed` to each structural subscriber with the previous value (looked up from `effectiveCache` or null).
  6. Replace `effectiveCache` with the per-variable map computed in step 1, and `lastKeySnapshot` with `newKeys`.

A key concern: the `IReadOnlyEntity.SubscribeToVariableStructuralChanges` signature passes a third argument of type `VariableValue?`. The exact semantics ("the new value for `Added`, the old value for `Removed`, or null") need to match `LocalVariableStorage`'s emission. Inspect `LocalVariableStorage.SubscribeToVariableStructuralChanges` and `VariableBag.OnVariableStructuralChange` at implementation time and mirror their convention precisely. If the precedent is "value at the time of structural change," the steps above are correct as written.

**Step 1.2 — Replace `StateEntity<TDef>` with the class form.**

Delete the existing `Assets/Packages/com.scaffold.entities.states/Runtime/StateEntity.cs` (the record) and `Runtime/EntityStateProvider.cs` (the aggregate provider) entirely. Create a new `Runtime/StateEntity.cs`:

    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public sealed class StateEntity<TDefinition> : BaseEntityInstance<TDefinition>, IMutableEntity<TDefinition>
            where TDefinition : IEntityDefinition
        {
            internal void InitializeStateBacked(InstanceId id, TDefinition definition, Store store, StoreVariableStorage storage)
            {
                this.store = store;
                Initialize(id, definition, storage);
            }

            private Store store = default!;

            public bool AddVariable(Variable key, VariableValue initialBase)
            {
                store.Execute(Id, new AddEntityVariablePayload(Id, key, initialBase));
                return true;
            }

            public bool RemoveVariable(Variable key)
            {
                // Wired in Milestone 2 with RemoveEntityVariablePayload.
                throw new System.NotImplementedException("Variable removal lands in Milestone 2 (Plans/EntityAsInstance).");
            }

            public ModifierId AddModifier(EntityModifierEntry entry)
            {
                ModifierId id = ModifierId.New();
                store.Execute(Id, new AddModifierPayload(Id, entry.Key, entry.Modifier, id));
                return id;
            }

            public bool RemoveModifier(Variable key, ModifierId id)
            {
                store.Execute(Id, new RemoveModifierPayload(Id, key, id));
                return true;
            }

            public void ClearModifiers()
            {
                // Compose from current state: enumerate all (variable, modifierId) pairs and dispatch removes in a batch.
                var snapshot = store.Get<EntityVariableState>(Id);
                var payloads = new System.Collections.Generic.List<object>();
                foreach (var kv in snapshot.ModifierStacks)
                {
                    foreach (var active in kv.Value)
                    {
                        payloads.Add(new RemoveModifierPayload(Id, kv.Key, active.Id));
                    }
                }
                if (payloads.Count > 0)
                {
                    store.ExecuteBatch(payloads);
                }
            }
        }
    }

The `Initialize` call on `BaseEntityInstance<TDefinition>` sets `Id`, `definition`, and `Storage`. The `[SerializeField]` attributes inherited from the base class are inert in the state-backed path (these instances are constructed in code, not authored), but harmless.

The `AddVariable` return value of `true` is a placeholder until the existing `AddEntityVariableMutator`'s no-op-on-existing-key behavior is reconciled with `IMutableEntity.AddVariable`'s contract (`bool` indicates whether the variable was actually added). Document this in `Surprises & Discoveries` if the existing test `AddEntityVariable_AddsRuntimeVariableThatWasNotInDefinition` reveals the contract gap; the simplest fix is to compare `EntityVariableState.BaseValues.ContainsKey(key)` before and after `Execute` — but that requires reading state twice across an Execute, which is awkward. An alternative is to upgrade the mutator to reject duplicate adds with a thrown exception, matching what `LocalVariableStorage.AddVariable` does. Resolve this in implementation and record the chosen convention in the Decision Log.

**Step 1.3 — Reshape `EntityStateFactory.Create`.**

Replace the body of `EntityStateFactory.Create<TDefinition>(TDefinition, Store, InstanceId)`:

    public static StateEntity<TDefinition> Create<TDefinition>(TDefinition definition, Store store, InstanceId instanceId)
        where TDefinition : IEntityDefinition
    {
        ValidateCreateArgs(definition, store, instanceId);
        store.RegisterSlice(instanceId, EntityVariableState.Empty);
        var storage = new StoreVariableStorage(store, instanceId, definition);
        var entity = new StateEntity<TDefinition>();
        entity.InitializeStateBacked(instanceId, definition, store, storage);
        return entity;
    }

The aggregate registration is removed; only the canonical slice is registered. The entity's lifetime is the responsibility of whoever called `Create` — typically a long-lived gameplay system that holds entity references in a registry keyed by `InstanceId`.

**Step 1.4 — Migrate the integration tests.**

Open `Assets/Packages/com.scaffold.entities.states/Tests/StateEntityIntegrationTests.cs`. The tests today read via `store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp)`. After this milestone, `StateEntity<TDef>` is no longer a state type and that call throws `KeyNotFoundException`.

For each test that does `var (store, _, _, id) = CreateEntity()`, change the destructuring to capture the entity reference: `var (store, _, entity, id) = CreateEntity()`. Read assertions become `entity.GetVariable<float>(hp)` directly.

The test `StateEntity_IsImmutableRecord_NotAssignableToMutableEntity` (lines 78–85) asserts that `StateEntity<EntityDefinition>` is *not* assignable to `IMutableEntity<EntityDefinition>`. After this plan, that assertion is exactly wrong — the new shape *is* assignable to `IMutableEntity<EntityDefinition>`. Replace this test with two compile-time-grade checks:

- `Assert.That(typeof(IReadOnlyEntity<EntityDefinition>).IsAssignableFrom(typeof(StateEntity<EntityDefinition>)), Is.True, "StateEntity must be assignable to IReadOnlyEntity for polymorphism with EntityInstance.");`
- `Assert.That(typeof(IMutableEntity<EntityDefinition>).IsAssignableFrom(typeof(StateEntity<EntityDefinition>)), Is.True, "StateEntity must be assignable to IMutableEntity since it owns mutation methods that translate to store payloads.");`

The test `Snapshot_DoesNotIncludeAggregateState` (lines 128–138) asserts the snapshot does not contain `typeof(StateEntity<EntityDefinition>)`. After this plan there is no aggregate state at all, so the assertion's intent is moot — but the assertion still passes as written (the snapshot indeed does not contain that type, because that type is no longer a state at all). Update the test name to `Snapshot_ContainsOnlyCanonicalEntityVariableState` and replace the assertion with `Assert.That(snapshot.Contains(id, typeof(EntityVariableState)), Is.True);` plus an explicit `Assert.That(<snapshot key count for this id>, Is.EqualTo(1));`.

The test `AggregateSubscription_FiresOnRebuild_WithFreshRecord` (lines 115–126) subscribes via `store.Subscribe<StateEntity<EntityDefinition>>(id, ...)`. After this plan, that subscription target does not exist. Rewrite the test to subscribe at the entity level:

    entity.Subscribe(hp, value => captured.Add((float)((IVariableValue<float>)value).Get()));

The assertion semantics (one fire per `Execute` that changes `hp`'s effective value, last value reflects fully-applied modifiers) carry over directly. Pay attention to the immediate-fire-on-subscribe behavior — the captured list will start with 1 entry from the subscribe call before any `Execute`, so adjust the count assertion (`Is.GreaterThanOrEqualTo(3)` instead of `2` to account for the initial fire).

The test `AggregateSubscription_FiresAfterLoadSnapshot` similarly needs to switch to `entity.Subscribe(hp, ...)`. The assertion that the post-LoadSnapshot callback observes `15f` (the snapshot's fully-applied state) holds.

All other tests change only the read assertion target — from `store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp)` to `entity.GetVariable<float>(hp)`. The mutation paths (`store.Execute(...)` with explicit payloads) continue to work and continue to be the supported way to mutate from outside the entity. The entity-level mutation path (`entity.AddModifier(entry)`) is also exercised in at least one new test added in this milestone.

Add a new test that exercises the entity-level mutation path and observes equivalence:

    [Test]
    public void EntityMutation_RoutesThroughStore_AndProducesSameStateAsDirectExecute()
    {
        var (storeA, _, entityA, idA) = CreateEntity();
        var (storeB, _, _, idB) = CreateEntity();

        entityA.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)));
        storeB.Execute(idB, new AddModifierPayload(idB, hp, new FloatAddModifier(5f), ModifierId.New()));

        var stateA = storeA.Get<EntityVariableState>(idA);
        var stateB = storeB.Get<EntityVariableState>(idB);

        Assert.That(stateA.ModifierStacks.ContainsKey(hp), Is.True);
        Assert.That(stateB.ModifierStacks.ContainsKey(hp), Is.True);
        Assert.That(stateA.ModifierStacks[hp].Count, Is.EqualTo(stateB.ModifierStacks[hp].Count));
        Assert.That(entityA.GetVariable<float>(hp), Is.EqualTo(15f));
    }

The two `ModifierId` values differ (one generated by `entityA.AddModifier`, one by the test), but the structural shape (stack length, ordering) and the observable effective values are identical.

**Acceptance for Milestone 1.** Run from the repository root:

    pwsh -NoProfile -File ".agents\scripts\run-editmode-tests.ps1" -TestPlatform EditMode

All 22 existing tests (after migration) and the one new test pass. `validate-changes.cmd` reports `TOTAL:0`.

### Milestone 2 — Variable removal (P0.1)

The goal is to ship the `RemoveVariable` write path end to end: payload, mutator, state method, entity method, regression test.

**Step 2.1 — Add `EntityVariableState.WithoutVariable(Variable)`.**

Append to `Runtime/EntityVariableState.cs`:

    public EntityVariableState WithoutVariable(Variable variable)
    {
        if (variable == null) throw new ArgumentNullException(nameof(variable));

        bool hasBase = BaseValues.ContainsKey(variable);
        bool hasStack = ModifierStacks.ContainsKey(variable);
        if (!hasBase && !hasStack) return this;

        var nextBases = hasBase ? CreateMutableValues(BaseValues) : null;
        if (nextBases != null) nextBases.Remove(variable);

        var nextStacks = hasStack ? CreateMutableStacks(ModifierStacks) : null;
        if (nextStacks != null) nextStacks.Remove(variable);

        return this with
        {
            BaseValues = nextBases ?? BaseValues,
            ModifierStacks = nextStacks ?? ModifierStacks
        };
    }

The single method clears both the base override and the entire modifier stack atomically. This matches Card-Framework's preferred default (their P0.1 spec asks: "Best default for us: clear it"). If the variable has neither a base override nor a modifier stack, return the same instance — `WithoutVariable` becomes a no-op rather than throwing, mirroring `WithoutModifier`'s convention for missing modifiers.

**Step 2.2 — Add `RemoveEntityVariablePayload`.**

Create `Runtime/Payloads/RemoveEntityVariablePayload.cs`:

    using Scaffold.Entities;

    namespace Scaffold.Entities.States
    {
        public sealed record RemoveEntityVariablePayload(InstanceId EntityId, Variable Variable);
    }

**Step 2.3 — Add `RemoveEntityVariableMutator`.**

Create `Runtime/Mutators/RemoveEntityVariableMutator.cs`:

    #nullable enable

    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        internal sealed class RemoveEntityVariableMutator : Mutator<EntityVariableState, RemoveEntityVariablePayload>
        {
            public override EntityVariableState Change(EntityVariableState state, RemoveEntityVariablePayload payload, IStateScope scope)
            {
                return state.WithoutVariable(payload.Variable);
            }
        }
    }

**Step 2.4 — Register the new mutator.**

Open `Runtime/EntityBridgeContext.cs` and add `store.RegisterMutator(new RemoveEntityVariableMutator());` after the existing four registrations.

**Step 2.5 — Wire `StateEntity<TDef>.RemoveVariable`.**

Replace the `NotImplementedException` body in `Runtime/StateEntity.cs`:

    public bool RemoveVariable(Variable key)
    {
        var snapshot = store.Get<EntityVariableState>(Id);
        bool wasPresent = snapshot.BaseValues.ContainsKey(key) || snapshot.ModifierStacks.ContainsKey(key);
        if (!wasPresent) return false;
        store.Execute(Id, new RemoveEntityVariablePayload(Id, key));
        return true;
    }

The pre-Execute read returns `false` for missing variables, matching the bool-returning contract on `IMutableEntity.RemoveVariable`. The post-Execute observation is implicit: the mutator's no-op short-circuit preserves the same instance, which is harmless.

**Step 2.6 — Regression test.**

Append to `Tests/StateEntityIntegrationTests.cs`:

    [Test]
    public void RemoveVariable_ClearsBaseAndModifiers_AndFallsBackToDefinitionDefault()
    {
        var (store, _, entity, id) = CreateEntity();
        var armor = new Variable("armor", "float");

        // Add a runtime variable, set base, attach modifier.
        store.Execute(id, new AddEntityVariablePayload(id, armor, new FloatVariableValue(7f)));
        store.Execute(id, new AddModifierPayload(id, armor, new FloatAddModifier(3f), ModifierId.New()));
        Assert.That(entity.GetVariable<float>(armor), Is.EqualTo(10f), "Sanity: 7 base + 3 modifier.");

        // Remove via the entity API.
        bool removed = entity.RemoveVariable(armor);
        Assert.That(removed, Is.True);

        // armor is not in the definition; it should now be unreadable.
        Assert.That(entity.TryGetVariable<float>(armor, out _), Is.False,
            "RemoveVariable must clear the runtime variable so reads fall through to the definition (which doesn't define it).");

        // hp is still defined and unaffected.
        Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));
    }

    [Test]
    public void RemoveVariable_OnDefinitionVariable_ClearsRuntimeOverridesButLeavesDefinitionDefault()
    {
        var (store, _, entity, id) = CreateEntity();

        store.Execute(id, new SetBaseValuePayload(id, hp, new FloatVariableValue(20f)));
        store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
        Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(25f));

        bool removed = entity.RemoveVariable(hp);
        Assert.That(removed, Is.True);

        // Definition still defines hp = 10f; runtime overrides are gone.
        Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f),
            "Removing a definition-defined variable clears runtime overrides but does not delete the definition default.");
    }

    [Test]
    public void RemoveVariable_OnUnknownVariable_ReturnsFalse()
    {
        var (_, _, entity, _) = CreateEntity();
        var unknown = new Variable("unknown", "float");
        Assert.That(entity.RemoveVariable(unknown), Is.False);
    }

**Acceptance for Milestone 2.** All three new tests plus all migrated Milestone 1 tests pass. `validate-changes.cmd` reports `TOTAL:0`.

### Milestone 3 — Documentation pass

The goal is to make the bridge package's surface decisions and recommended patterns visible at the package root.

**Step 3.1 — Author `README.md`.**

Create `Assets/Packages/com.scaffold.entities.states/README.md`. It must cover:

- One-paragraph orientation: this package gives you a `StateEntity<TDef>` that is an `EntityInstance` whose variable storage lives in a `Scaffold.States.Store`. Reads use `IReadOnlyEntity<TDef>`. Writes go through `store.Execute(payload)` or the equivalent method on `IMutableEntity<TDef>`.
- Quickstart: code sample showing definition creation, store builder, factory call, mutation, snapshot/restore round-trip.
- "Atomic multi-payload commit": call out `Store.ExecuteBatch(IReadOnlyList<object> payloads)` as the all-or-nothing primitive (P0.3 answered). Document semantics: all payloads share a single `MutatorRunner` overlay; if any payload's mutator throws, the overlay is discarded via `OnReturnedToPool → scratchpad.Reset()` and no partial state is committed.
- Subscription patterns:
  - Per-variable on a specific entity: `entity.Subscribe(variable, onChange)`. Fires immediately with the current value, then on each subsequent change to that variable's effective value. Skips notifications when the effective value is unchanged.
  - Whole-entity changes on a specific id: `store.Subscribe<EntityVariableState>(id, (ref, state, evt) => ...)`. Lower-level; receives the canonical slice.
  - Across all state-backed entities: `store.SubscribeAllReferences<EntityVariableState>((ref, state, evt) => ...)`. For "any state-backed entity changed" semantics; consumers filter on the receiving end.
- Effective-value caching: cached on canonical change inside `StoreVariableStorage`. Reads from a `StateEntity<TDef>` (via `IReadOnlyEntity<TDef>`) hit the cache; the cache is recomputed once per `Execute` that mutates the entity, regardless of how many subscribers exist.
- Modifier ordering: the bridge's modifier stack is per-`Variable`, ordered by `Modifier.Order` ascending, with insertion-order tiebreak (stable per insertion). Reserved Order ranges per logical layer is the recommended pattern for MTG-style continuous-effect layering.
- Bootstrap: call `EntityBridgeContext.RegisterMutators(store)` once per store before creating any state-backed entity. A second call throws `DuplicateMutatorRegistrationException`.
- Snapshot semantics: `EntityVariableState` slices round-trip; `StateEntity<TDef>` instances are not state and are not in snapshots. `StateEntity` references continue to read correctly after `LoadSnapshot` because they are `(store, id, def)` handles whose storage transparently re-reads the restored slice. Pruned entities (created after a snapshot was taken) lose their canonical slice on `LoadSnapshot`; subsequent reads through the entity will throw `KeyNotFoundException`.
- Known limitation: `SubscribeToVariableStructuralChanges` infers `Added`/`Removed` events by diffing the key set across rebuilds. A single `ExecuteBatch` containing both a remove and a re-add of the same variable yields no net structural change and emits nothing. Consumers that need per-mutation structural granularity should subscribe to canonical `EntityVariableState` updates directly and inspect the diff themselves.

The README is consumer-facing documentation and should not exceed two screens of plain prose.

**Acceptance for Milestone 3.** The README exists, the validation command reports `TOTAL:0`, and a fresh reader can follow the quickstart end-to-end without consulting any other file.


## Concrete Steps

All commands run from `c:\Unity\Scaffold` unless stated otherwise.

Validation command — run after completing every milestone before committing:

    .agents\scripts\validate-changes.cmd

EditMode tests — run before the final commit of each milestone:

    pwsh -NoProfile -File ".agents\scripts\run-editmode-tests.ps1" -TestPlatform EditMode

When creating new files under `Assets/`, use Unity MCP to ensure `.meta` files are generated with valid GUIDs. If Unity MCP is unavailable, copy the `.meta` file shape from a sibling file (for example, copy `Runtime/Mutators/AddModifierMutator.cs.meta` to `Runtime/Mutators/RemoveEntityVariableMutator.cs.meta`) and replace the `guid` value with a fresh 32-character hex string generated by `[System.Guid]::NewGuid().ToString("N")` from PowerShell.

When deleting files (`StateEntity.cs` record form, `EntityStateProvider.cs`), delete the `.meta` files alongside them.


## Validation and Acceptance

The change is complete when all of the following hold:

1. `validate-changes.cmd` reports `TOTAL:0` from the repository root.
2. All tests in `Scaffold.Entities.States.Tests` pass, including the migrated Milestone 1 set, the entity-level mutation parity test, and the three Milestone 2 variable-removal tests.
3. The runtime type returned by `EntityStateFactory.Create<TDefinition>(...)` is assignable to both `IReadOnlyEntity<TDefinition>` and `IMutableEntity<TDefinition>`. Verify by adding to the factory test:

       var entity = EntityStateFactory.Create(new EntityDefinition(), store, new InstanceId(1));
       IReadOnlyEntity<EntityDefinition> readHandle = entity;
       IMutableEntity<EntityDefinition> mutableHandle = entity;
       Assert.That(readHandle.Id, Is.EqualTo(mutableHandle.Id));

4. The end-to-end snapshot round-trip test passes against the new shape:

       var (store, _, entity, id) = CreateEntity();
       var snapshot = store.SaveSnapshot();
       entity.AddModifier(new EntityModifierEntry(hp, new FloatAddModifier(5f)));
       Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(15f));
       store.LoadSnapshot(snapshot);
       Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));

5. A subscriber registered via `entity.Subscribe(hp, onChange)` fires once at subscribe time with the current value, fires when `hp`'s effective value changes through any path (entity-level `AddModifier` or store-level `Execute`), and does not fire when only `attack` (a different variable on the same entity) changes.

6. The `StateEntity<TDef>` type does *not* appear in `store.SaveSnapshot()` output for any entity, because it is not a state type. Verify by re-running `Snapshot_ContainsOnlyCanonicalEntityVariableState` from the migrated test suite.

7. The bridge package contains exactly one state type (`EntityVariableState`) and zero `AggregateState` subclasses. Verify by file inspection: `Assets/Packages/com.scaffold.entities.states/Runtime/EntityStateProvider.cs` does not exist, and the only `: State` declaration in the package is on `EntityVariableState`.


## Idempotence and Recovery

Each milestone ends in a compilable, test-passing state. If work is interrupted mid-milestone, restore to the last clean commit and re-apply only the steps that were not completed. No step destructively modifies Unity asset GUIDs — file renames preserve `.meta` files. If a rename was partially applied (new file exists but old file also exists), delete the old file and its `.meta` manually and re-run validation.

The only destructive operation in this plan is the deletion of `Runtime/StateEntity.cs` (record form) and `Runtime/EntityStateProvider.cs`. Both are recoverable from `git history` (specifically, commit `da6cf58` on `feature/entities-state-bridge`). Before deleting either file in Milestone 1, confirm the new class form of `StateEntity<TDef>` and `StoreVariableStorage` compile alongside the old types — i.e., add the new files first, get them compiling, then delete the obsolete ones in a single follow-up edit. Do not commit a state where both shapes coexist.


## Artifacts and Notes

Expected output from `validate-changes.cmd` after each milestone:

    Change Validation Summary
    ----------------------------
    Scripts asmdef audit: PASS (TOTAL:0)
    Compilation: PASS (exit code 0)
    Analyzers: PASS (TOTAL:0, BLOCKERS:0)

If the analyzer reports `SCA3002` (one type per file) on any new file, split per-class. The Milestone 1 file `StoreVariableStorage.cs` contains exactly one class so this should not trigger; the helper `CallbackDisposable` and `EmptyDisposable` types are imported from `Scaffold.Entities` and not redefined here.

If `EntityModifierEntry` does not yet have a public constructor compatible with `(Variable, VariableModifier)` — verify by inspecting `Assets/Packages/com.scaffold.entities/Runtime/Core/Definitions/EntityModifierEntry.cs` at implementation time — either use the existing constructor shape or document the gap and add a minimal public constructor in the entities package (record this in the Decision Log).


## Interfaces and Dependencies

Final public signatures that must exist at the end of this plan.

In `Assets/Packages/com.scaffold.entities.states/Runtime/StateEntity.cs`:

    public sealed class StateEntity<TDefinition> : BaseEntityInstance<TDefinition>, IMutableEntity<TDefinition>
        where TDefinition : IEntityDefinition
    {
        public bool AddVariable(Variable key, VariableValue initialBase);
        public bool RemoveVariable(Variable key);
        public ModifierId AddModifier(EntityModifierEntry entry);
        public bool RemoveModifier(Variable key, ModifierId id);
        public void ClearModifiers();
        // All read methods inherited from BaseEntityInstance<TDefinition>.
    }

In `Assets/Packages/com.scaffold.entities.states/Runtime/EntityStateFactory.cs` (signature unchanged from current; body reshaped):

    public static class EntityStateFactory
    {
        public static StateEntity<TDefinition> Create<TDefinition>(TDefinition definition, Store store, InstanceId instanceId)
            where TDefinition : IEntityDefinition;
    }

In `Assets/Packages/com.scaffold.entities.states/Runtime/StoreVariableStorage.cs` (new, internal):

    internal sealed class StoreVariableStorage : IEntityVariableStorage
    {
        public StoreVariableStorage(Store store, InstanceId id, IEntityDefinition definition);
        // IEntityVariableStorage members: TryGetEffective, TryGetBase, Variables, Subscribe, Unsubscribe, SubscribeToVariableStructuralChanges.
    }

In `Assets/Packages/com.scaffold.entities.states/Runtime/Payloads/RemoveEntityVariablePayload.cs` (new):

    public sealed record RemoveEntityVariablePayload(InstanceId EntityId, Variable Variable);

In `Assets/Packages/com.scaffold.entities.states/Runtime/EntityVariableState.cs` (existing, with one method appended):

    public EntityVariableState WithoutVariable(Variable variable);

In `Assets/Packages/com.scaffold.entities.states/Runtime/EntityBridgeContext.cs` (existing; one extra mutator registered):

    public static class EntityBridgeContext
    {
        public static void RegisterMutators(Store store);
        // Now registers: AddModifierMutator, RemoveModifierMutator, SetBaseValueMutator, AddEntityVariableMutator, RemoveEntityVariableMutator.
    }

Removed types (must not exist in the package after this plan):

- `StateEntity<TDef>` as a `record : AggregateState`.
- `EntityStateProvider<TDef> : AggregateProvider<StateEntity<TDef>>`.

The bridge package's runtime asmdef references stay `Scaffold.Entities` and `Scaffold.States`. The tests asmdef stays `Scaffold.Entities.States`, `Scaffold.Entities`, `Scaffold.States`, `nunit.framework`. `package.json` is unchanged.


## Revision history

- **2026-04-29** — Initial ExecPlan authored from design session reversing `Plans/EntityAsAggregate/EntityAsAggregate-ExecPlan.md` after Card-Framework consumer review identified that the aggregate-record `StateEntity<TDef>` is a parallel read shape rather than an `IReadOnlyEntity<TDef>` implementation. Plan restores the `BaseEntityInstance<TDef>` subclass shape and bundles P0.1 (variable removal). P0.2 (modifier source) and P0.3 (atomic multi-payload commit, already satisfied by `Store.ExecuteBatch`) are out of scope.
