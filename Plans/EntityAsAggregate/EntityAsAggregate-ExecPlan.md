# Entity as aggregate: state-backed entities are live aggregate slices

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. Repository policy for ExecPlans is defined in `PLANS.md` at the repository root; this document is maintained in accordance with that file.

This plan supersedes `Plans/StatelessEntityBridge/StatelessEntityBridge-ExecPlan.md`. That earlier follow-up to `Plans/StateBackedEntities/StateBackedEntities-ExecPlan.md` proposed a halfway design — definitions stored as a side slice, mutators looking them up via `IStateScope.Get<EntityDefinitionState>` — that solved the static-singleton smell but kept the bridge writing its own effective-value cache and inventing an auxiliary slice purely for definition plumbing. Research into `Scaffold.States.AggregateSlice` (see `Surprises & Discoveries` below) showed that the framework already has the right primitive for "an addressable thing whose state is a derived view of authored data, kept in sync automatically." Using that primitive, the entire bridge collapses into three roles cleanly separated: `InstanceId` (the serializable reference), `StateEntity<TDef>` (the immutable derived view, an `AggregateState` record), and `EntityStateProvider<TDef>` (the dedicated provider that builds the view from authored state). Mutators only write authored data; the framework rebuilds the rest.


## Purpose / Big Picture

Today, the bridge package `Assets/Packages/com.scaffold.entities.states` works by storing a single `EntityVariableState(BaseValues, ModifierStacks, EffectiveValues)` record per entity, keyed by an `InstanceId(int Id)` reference. Mutators receive an `EntityBridgeContext` constructor argument — a per-`Store` singleton tracked in a static `ConditionalWeakTable<Store, EntityBridgeContext>` — and use it to resolve the entity's `IEntityDefinition` so they can recompute the cached `EffectiveValues` field on every write. Reads through `StoreVariableStorage` consult `state.EffectiveValues` (or fall back to base values and the definition's defaults). All the bridge-internal scaffolding exists to keep this cache fresh.

After this plan, three roles are made explicit and separate:

- **`InstanceId(int Id) : IReference`** — the durable, serializable handle. It is the slice key in the store and the field on payloads. Networked play, save files, lookup tables, and debug logs all use it. It survives loss of the runtime entity reference.
- **`StateEntity<TDef> : AggregateState`** — the rich, immutable snapshot of an entity at a point in time. It is a `record` produced by the framework on every authored-state change. It carries `Id`, `Definition`, the resolved `BaseValues`, the ordered `ModifierStacks`, and the cached `EffectiveValues`, plus convenience read methods (`GetVariable<T>`, `TryGetVariable<T>`). Gameplay code reads it via `store.Get<StateEntity<TDef>>(id)` or receives it through subscriptions.
- **`EntityStateProvider<TDef> : AggregateProvider<StateEntity<TDef>>`** — the dedicated provider class. Its `Build` reads the authored `EntityVariableState` source slice for one entity and folds modifiers using the entity's intrinsic `Definition` to produce the next `StateEntity<TDef>` record. Its `Wire` subscribes to changes on that source slice. The provider is the only behavior-rich object in the bridge; the gameplay-facing types are records and value-like records.

The store's existing aggregate-slice machinery does the rest:

- The canonical (mutated) slice shrinks to `EntityVariableState(BaseValues, ModifierStacks)` — pure authored data. Mutators write here and only here.
- The aggregate slice is keyed by `InstanceId` and holds a `StateEntity<TDef>` produced by an `EntityStateProvider<TDef>`. The framework rebuilds it whenever the source changes, after `LoadSnapshot`, or on demand inside a mutator scratchpad.
- `EntityBridgeContext` reduces to a single static helper, `RegisterMutators(Store store)`, that whoever sets up the store calls once. There is no per-store instance state; no `ConditionalWeakTable`; no lock; no `Dictionary<InstanceId, IEntityDefinition>`; no `EffectiveValueRecomputer`.
- Subscriptions become first-class: `store.Events.Subscribe<StateEntity<TDef>>(id, callback)` is the per-entity case directly. Per-variable fan-out, if needed, is a thin adapter on top.

A novice reader can see this working through three observable changes:

1. The size of `Assets/Packages/com.scaffold.entities.states/Runtime/EntityBridgeContext.cs` drops from roughly 45 lines to roughly 12.
2. `store.Get<StateEntity<HeroDef>>(heroId).GetVariable<float>(hp)` returns the entity's current effective value. Calling `store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatAddModifier(5f), ModifierId.New()))` causes the next `store.Get<StateEntity<HeroDef>>(heroId)` to return a freshly-built record with the new effective value, and an `Updated` notification fires through the standard event handler.
3. `Snapshot.SaveSnapshot()` captures only `EntityVariableState` slices; `StateEntity<TDef>` aggregate values do not appear in the snapshot. After `LoadSnapshot`, reading the entity returns the value derived from the restored authored data — proven by an explicit regression test added in this plan.

The user-facing API shifts compared to today. Today, callers hold a runtime `StateEntity` *class* with a live storage object; reading variables always returns the current value because the storage transparently consults the latest store state. After this plan, `StateEntity<TDef>` is a *record* — an immutable snapshot — so a held reference reflects the values at the time the snapshot was produced. To see the current value, callers either re-fetch via `store.Get<StateEntity<TDef>>(id)` or subscribe to changes and react to each new record. This is a deliberate and visible change; the convenience of "always-fresh reads through a stable handle" is traded for the rigor of "every read is an explicit snapshot at a moment in time." See `Decision Log` for the rationale.

Neither core package gains a circular dependency. The bridge depends on `Scaffold.Entities`, `Scaffold.States`, and `Scaffold.Records` (the last for `IsExternalInit`). `Scaffold.States` gains one small surface addition — a runtime `Store.RegisterAggregate(IReference, IAggregateProvider)` symmetric to today's `Store.RegisterSlice(IReference, State)` — because state-backed entities are usually created at runtime, not during `StoreBuilder` configuration.


## Progress

- [x] Milestone 1 — `Scaffold.States` gains a runtime `Store.RegisterAggregate(IReference, IAggregateProvider)` method (and a regression test). After this milestone, an aggregate slice can be installed onto a Store after `Build()` returns. **Implemented in [Store.cs:317](Assets/Packages/com.scaffold.states/Runtime/Store.cs:317); covered by [StoreRegisterAggregateTests.cs](Assets/Packages/com.scaffold.states/Tests/StoreRegisterAggregateTests.cs) (3 cases: read-after-build, rebuild-on-canonical-change, rebuild-after-load-snapshot).**
- [x] Milestone 2 — Bridge refactor: introduced `StateEntity<TDef> : AggregateState` (record) and `EntityStateProvider<TDef> : AggregateProvider<StateEntity<TDef>>` (provider class); shrunk `EntityVariableState` to `(BaseValues, ModifierStacks)` and added behavior methods (`WithModifier`, `WithoutModifier`, `WithBaseValue`, `WithVariable`, `ResolveEffectiveValues`); mutators are one-line translations from payload to record method call; `EntityBridgeContext` collapsed to a static `RegisterMutators(Store)` helper; `EntityStateFactory.Create` returns the initial `StateEntity<TDef>` record after registering source slice and aggregate provider. The bridge's class-based `StateEntity<TDef> : BaseEntityInstance<TDef>` was retired. `StoreVariableStorage` and `EffectiveValueRecomputer` are deleted.
- [x] Milestone 3 — `MutatorRegistry` dedupe: `Scaffold.States.MutatorRegistry.Register` throws `DuplicateMutatorRegistrationException` when called twice with the same `(TState, TPayload, mutator concrete runtime type)`. Implemented in [MutatorRegistry.cs:41](Assets/Packages/com.scaffold.states/Runtime/Pipeline/MutatorRegistry.cs:41); regression test in [MutatorRegistryDeduplicationTests.cs](Assets/Packages/com.scaffold.states/Tests/MutatorRegistryDeduplicationTests.cs).
- [x] Final validation gate — analyzer pass `BUILD_EXIT:0`, `TOTAL:0`. EditMode tests previously verified green by user prior to post-implementation review.
- [x] Post-implementation cleanup (2026-04-29) — deleted stale empty `Compatibility/` folder, removed unused `AssemblyInfo.cs` (no test reaches into bridge internals after the refactor), replaced the structurally vacuous `StateEntity_DoesNotImplement_IMutableEntity` test with a stronger `StateEntity_IsImmutableRecord_NotAssignableToMutableEntity`, added five missing integration tests (`SetBaseValue_*`, `AddEntityVariable_*`, `AggregateSubscription_FiresOnRebuild_WithFreshRecord`, `Snapshot_DoesNotIncludeAggregateState`, `TwoEntities_SnapshotRoundTrip_RebuildsBothAggregates`), added `Scaffold.Maps` reference to the bridge tests asmdef so `Snapshot.Contains` is reachable.


## Surprises & Discoveries

- **`AggregateSlice` is built for exactly this use case.** Reading the framework code end-to-end (`Assets/Packages/com.scaffold.states/Runtime/State/AggregateSlice.cs`, `IAggregateProvider.cs`, `Store.cs`) revealed that aggregate slices have these properties: (a) the provider's `Build(IStateScope scope)` returns an `AggregateState` record built from source slices via `scope.Get<...>` calls, and the slice rebuilds whenever its `Wire` callback fires `RequestRebuild`; (b) inside a mutator's scratchpad, `Get<TState>(reference)` for an aggregate calls `aSlice.BuildForScope(this)` so reads see the in-flight overlay, not stale canonical state; (c) `Store.SaveSnapshot()` iterates only the canonical `map` field, never `aggregates` — aggregates are not part of snapshots; (d) on `LoadSnapshot`, the canonical changes fire `Updated` and `Removed` events, which trigger every wired aggregate to rebuild. The existing tests `AggregateKeyedCanonicalTests.Aggregate_Rebuilds_When_KeyedCanonical_Created` and `Aggregate_Rebuilds_After_LoadSnapshot_Prune_RemovesKeyedCanonical` already prove these behaviors in the framework's test suite.

- **`Store.RegisterAggregate` does not exist post-build.** `StoreBuilder` has `RegisterAggregate(IReference, IAggregateProvider)` for build-time wiring, but the runtime `Store` does not. `Store.RegisterSlice(...)` exists for canonical state. We need the symmetric `Store.RegisterAggregate(...)` to install an aggregate after `Build()`, since state-backed entities are typically created at runtime.

- **The provider class is allowed to be mutable; the aggregate state it produces must not be.** Reading `AggregateProvider<TAggregate>` and the `RebuildCallback` machinery, the framework only interacts with the provider through three methods (`AggregateStateType`, `Build`, `Wire`). The `AggregateState` records the provider produces are immutable. The provider object itself can hold definition references, attached-store callbacks, and any other behavior-rich state; the framework does not care. This decouples the question "is the provider mutable?" (yes, fine) from the question "is the state record immutable?" (yes, required).

- **Conflating "the provider" and "the gameplay handle" muddies both roles.** The previous draft of this plan had `StateEntity<TDef>` implement both `IReference` and `IAggregateProvider` while also being the gameplay-facing read API. That made one type carry three roles: identity, behavior, and data. Splitting into three types — `InstanceId` for identity, `EntityStateProvider<TDef>` for behavior, `StateEntity<TDef>` (record) for data — keeps each type single-purpose. The price is that gameplay reads become snapshot-oriented (`store.Get<StateEntity<TDef>>(id).GetVariable<...>` instead of `entity.GetVariable<...>` against a live handle), which is a visible API change but a more honest one given that the data is genuinely a derived snapshot, not a live mutable object.


## Decision Log

- Decision: state-backed entities are split into three types: `InstanceId : IReference` (identity), `StateEntity<TDef> : AggregateState` (the record produced by the aggregate), and `EntityStateProvider<TDef> : AggregateProvider<StateEntity<TDef>>` (the dedicated provider class). Rationale: separates identity from behavior from data; each type is single-purpose; matches the framework's three-role model (`IReference`, `IAggregateProvider`, `AggregateState`); makes the immutability invariant a property of the data type, not a discipline imposed on a multi-role class. Author: design conversation, 2026-04-29.
- Decision: `InstanceId` remains the slice key and the durable handle. Rationale: serializable identity is required for networked play, save/load translation, lookup tables, and any context where the runtime aggregate record could be discarded. Promoting the runtime entity to be the reference would have lost this. The framework keys slices by `InstanceId`; the runtime aggregate (`StateEntity<TDef>` record) is just data attached to that key. Author: design conversation, 2026-04-29.
- Decision: `StateEntity<TDef>` is a `record` extending `AggregateState`, not a class. Rationale: the framework requires `AggregateState` to be a record (it inherits from `record BaseState`); the record's structural-equality semantics give the framework a clean way to detect rebuild-vs-no-change; the immutability is guaranteed by the record contract; gameplay code that wants to keep a "current view" can either re-fetch from the store on each read or subscribe to receive each new record. Convenience read methods (`GetVariable<T>`, `TryGetVariable<T>`) are added to the record so callers do not have to reach into raw dictionaries. Author: design conversation, 2026-04-29.
- Decision: the previous bridge `StateEntity<TDef> : BaseEntityInstance<TDef>` class is retired. Rationale: with the data being a record and the provider being a separate class, there is nothing left for the previous class to hold. Standalone, non-state-backed entities continue to use `BaseEntityInstance<TDefinition>` and `EntityInstance<TDefinition>` from `com.scaffold.entities` unchanged — the bridge's API divergence is intentional. Author: design conversation, 2026-04-29.
- Decision: `Scaffold.States.Store` gains a public `RegisterAggregate(IReference, IAggregateProvider)` method symmetric to `RegisterSlice`. Rationale: today aggregate registration is build-time only via `StoreBuilder.RegisterAggregate`, but state-backed entities are typically created at runtime via `EntityStateFactory.Create`. The framework already supports post-build canonical-slice registration; aggregates need the symmetric capability. Author: research notes, 2026-04-29.
- Decision: `MutatorRegistry` dedupes by `(TState, TPayload, mutator concrete runtime type)` and throws `DuplicateMutatorRegistrationException` on collision. Rationale: independent safety improvement carried over from the superseded plan; protects future consumers of `Scaffold.States` from the dispatch foot-gun the bridge originally tripped on. Concrete-type dedupe would have caught the original bug (two distinct instances of the same class registered for the same payload type) while preserving legitimate fan-out across distinct mutator classes. Author: design conversation, 2026-04-29.
- Decision: this plan supersedes `Plans/StatelessEntityBridge/StatelessEntityBridge-ExecPlan.md`. The earlier plan converged on definition-as-side-slice; the present plan delivers the same outcome (entity owns its definition, no bridge bookkeeping) by using the framework primitive that already exists for live derived state, which removes more code and machinery than the earlier plan. Author: design conversation, 2026-04-29.
- Decision: `EntityVariableState` carries behavior methods (`WithModifier`, `WithoutModifier`, `WithBaseValue`, `WithVariable`, `ResolveEffectiveValues`) rather than being an anemic data record whose modifications are written out long-form inside each mutator. Rationale: immutability and behavior are not opposed — records can have methods; pushing the bookkeeping (copy-on-write of dictionaries, bucket copying, ordered insertion, the modifier fold) into the record collapses each mutator's `Change` into a one-line translation from payload to record method call, which is much closer to the framework's intent. Mutators stay focused on dispatch; the record owns "what it means to change me." The fold that produces effective values is exposed as `ResolveEffectiveValues(IEntityDefinition definition)` so the provider's `BuildCore` is a one-line orchestration as well. Author: design conversation, 2026-04-29.


## Outcomes & Retrospective

**What shipped.** All three milestones landed as designed. The bridge package now has a clean three-role decomposition: `InstanceId : IReference` is the durable handle, `StateEntity<TDef> : AggregateState` is the immutable derived record carrying `(Id, Definition, BaseValues, ModifierStacks, EffectiveValues)` and convenience read methods, and `EntityStateProvider<TDef> : AggregateProvider<StateEntity<TDef>>` is the dedicated builder that produces fresh `StateEntity` records whenever the source slice changes. Mutators each became a single expression delegating to a method on `EntityVariableState`. `EntityBridgeContext` is a 20-line static utility with no per-store state, no locks, no `ConditionalWeakTable`. `EffectiveValueRecomputer` and `StoreVariableStorage` are deleted; their concerns are absorbed by `EntityVariableState.ResolveEffectiveValues` and the framework's aggregate-event subscription path respectively.

**What we measured.** The analyzer pass is clean. The four bridge mutators are 1-line `Change` bodies. `Store.RegisterAggregate` is ~15 lines. `MutatorRegistry` dedupe is ~10 lines. Final test count: 11 integration tests in `Scaffold.Entities.States.Tests` (up from 9 originally proposed), 3 register-aggregate tests in `Scaffold.States.Tests`, 1 dedupe test in `Scaffold.States.Tests`. Tests cover all four payload paths, snapshot semantics for both authored and aggregate state, multi-entity isolation, multi-entity snapshot round-trip, and the aggregate-event subscription replacement for the deleted `StoreVariableStorage`.

**What worked well.**
- Pushing behavior onto `EntityVariableState` (the `WithModifier` / `WithoutModifier` / etc. methods) collapsed each mutator into a single line and made the record's invariants self-documenting. The data record owns "what it means to change me"; the mutator owns "what payload corresponds to what change."
- Splitting identity (`InstanceId`), data (`StateEntity` record), and behavior (`EntityStateProvider`) made each type single-purpose. `StateEntity` being a record gave value-equality and immutability for free; `EntityStateProvider` being a class made the per-entity definition reference natural. Neither type has to fight its own contract.
- Using the framework's `AggregateSlice` primitive directly was a strict win: snapshots are smaller (aggregates are not snapshotted), `LoadSnapshot` cleanup is automatic, no manual cache invalidation in mutators.

**Surprises during implementation.**
- The csproj for `Scaffold.Entities.States.Tests` had to gain a `Scaffold.Maps` project reference for `Snapshot.Contains(IReference, Type)` to compile (the method comes from the `Map<,,>` base class in `Scaffold.Maps`). Asmdef change alone wasn't sufficient because the csproj is generated from a different toolchain path; the manual ProjectReference addition is what unblocked the build.
- The `SCA2004` analyzer rejected two of the three pure helpers in `EntityVariableState` when marked `static` because their names (`IndexOfModifierInBucket`, `ComputeModifierInsertIndex`) don't match the allowed `Build*`/`Create*`/`New*`/`From*`/`To*`/`Parse*` prefixes. Reverted to instance methods rather than rename. Minor stylistic loss; not worth a fight.

**Follow-up candidates (not in scope here).**
- Per-variable subscription adapter on top of `store.Events.Subscribe<StateEntity<TDef>>(id, ...)` — useful for UI bindings that only care about a single variable. Was deliberately deleted with `StoreVariableStorage`; if a real consumer needs it, build it as a small filter on top of the aggregate event.
- A sample scene/script under `Samples~` showing the bridge in actual gameplay use. The README has a usage snippet, but a runnable sample would help future users.
- `Plans/StatelessEntityBridge/` is still on disk; it declares itself superseded internally but the folder remains. Either move it under an `Archive/` folder or add a top-level "SUPERSEDED" marker in the filename.


## Context and Orientation

This section explains everything a reader needs to know about both packages to implement the refactor. Read it fully before touching any code. Repository root for all paths is `C:\Unity\Scaffold` on Windows; paths below are repository-relative.

### What `com.scaffold.entities.states` looks like today

The bridge package lives at `Assets/Packages/com.scaffold.entities.states/`. Its runtime asmdef declares dependencies on `Scaffold.Entities`, `Scaffold.Records` (for `IsExternalInit`), and `Scaffold.States`. The package contains:

- `Runtime/EntityVariableState.cs` — a `Scaffold.States.State` record `(BaseValues, ModifierStacks, EffectiveValues)` keyed by `InstanceId`. Holds instance-level base overrides, ordered modifier stacks, and a cached effective view. Fields are `IReadOnlyDictionary` / `IReadOnlyList`; mutators copy-on-write through `CreateMutableValues` and `CreateMutableStacks` helpers.
- `Runtime/EntityBridgeContext.cs` — a per-`Store` singleton tracked in a static `ConditionalWeakTable<Store, EntityBridgeContext>` plus a lock. Each instance holds a `Dictionary<InstanceId, IEntityDefinition>` and registers four mutators on the store the first time it is requested. This plan dismantles it.
- `Runtime/EntityStateFactory.cs` — a public static class with `Create<TDef>(TDef definition, Store store, InstanceId instanceId)`. Today it calls `EntityBridgeContext.CreateForStore(store)`, then `context.Bind(instanceId, definition)`, then `store.RegisterSlice(instanceId, EntityVariableState.Empty)`, then builds a `StoreVariableStorage` and a `StateEntity` class.
- `Runtime/StateEntity.cs` — a sealed subclass of `Scaffold.Entities.BaseEntityInstance<TDef>`. It exposes only the read API and writes through the store via `store.Execute(...)`. Today it holds an `InstanceId Id`, a `TDef Definition`, and an `IEntityVariableStorage Storage`. **This class is replaced in Milestone 2 by a record of the same name.**
- `Runtime/StoreVariableStorage.cs` — implements `IEntityVariableStorage`. Subscribes once to the store's `EntityVariableState` events for its entity, fans those events out to per-variable callbacks, and serves reads against the stored slice. **Deleted in Milestone 2** — its job (subscribe to a slice, dispatch on change) is now subsumed by aggregate subscriptions on `StateEntity<TDef>`.
- `Runtime/Mutators/AddModifierMutator.cs`, `RemoveModifierMutator.cs`, `SetBaseValueMutator.cs`, `AddEntityVariableMutator.cs` — each takes an `EntityBridgeContext` constructor argument, looks up the entity's definition through it, and recomputes effective values inline.
- `Runtime/Mutators/EffectiveValueRecomputer.cs` — a static helper that builds the next `EffectiveValues` dictionary by folding the modifier stack for one variable. Called from each mutator. **Deleted in Milestone 2.**
- `Runtime/Payloads/AddModifierPayload.cs`, etc — `public sealed record` types carrying `InstanceId EntityId` plus the operation-specific data. Unchanged by this plan.
- `Tests/StateEntityIntegrationTests.cs` — nine tests including `Snapshot_RoundTripsModifierStack`, `TwoEntities_AddModifierAppliesOnceToTargetOnly`, `Modifier_OrderIsRespected_AddBeforeMultiply`, and the dispatch-verification test `DuplicateMutatorRegistration_AppliesPayloadTwice`. Tests are adapted in Milestone 2.

### What `Scaffold.States` provides for derived state

`Scaffold.States` defines several types you will use directly:

- `IReference` — a marker interface. Anything that needs to identify a slice in the store implements it. `Map<IReference, ...>` keys by `obj.Equals` and `obj.GetHashCode`; for record types, structural equality is automatic.
- `BaseState` — abstract base for state records.
- `State` — abstract base for canonical (mutated) state records, the kind targeted by mutators.
- `AggregateState : BaseState` — abstract `record` base for derived state records produced by an `IAggregateProvider`. Always a record.
- `IAggregateProvider` — three members: `Type AggregateStateType { get; }`, `void Wire(IStoreScope scope, IAggregateRebuild rebuild)`, `BaseState Build(IStateScope scope)`. The convenience base `AggregateProvider<TAggregate> : IAggregateProvider` exposes `protected abstract TAggregate BuildCore(IStateScope)`.
- `AggregateSlice : BaseSlice<AggregateState>` — wraps the provider, holds a reference to the attached store, exposes `BuildForScope(IStateScope)` for in-flight scratchpad reads, and calls `attachedStore.Events.Notify(Reference, aggregate, Updated)` whenever it rebuilds.
- `IStateScope` — `Get<TState>(reference)` interface seen by mutators and providers. The runtime `Store` implements this; the in-flight `Scratchpad` implements this and overlays in-flight changes from the current `Execute`.
- `IStoreScope : IStateScope` — adds `IStateEventHandler Events`. Used by `Wire` to subscribe to source-slice changes.
- `IStateEventHandler` — `Subscribe<TState>(IReference, Action<...>)` and `SubscribeAllReferences<TState>(...)` for typed subscriptions; `Notify(IReference, BaseState, StateChangeEvent)` for dispatch.
- `Store.SaveSnapshot()` and `Store.LoadSnapshot(snapshot)` — snapshots iterate only the canonical `map` field. Aggregates are not in snapshots; they rebuild from canonical slices via their `Wire` subscriptions when `LoadSnapshot` fires `Updated`/`Removed` events.
- `StoreBuilder.RegisterAggregate(IReference, IAggregateProvider)` — build-time aggregate registration. The runtime `Store` does not currently expose an equivalent post-build method; Milestone 1 of this plan adds it.

The existing test `Assets/Packages/com.scaffold.states/Tests/AggregateKeyedCanonicalTests.cs` demonstrates the aggregate flow in a self-contained way: a `KeyedCountersSumAggregateProvider` subscribes to all `CounterState` slices via `scope.Events.SubscribeAllReferences<CounterState>(...)`, builds a `KeyedCountersSumState(int Sum)` aggregate, rebuilds when canonical slices are added or removed, and survives `LoadSnapshot` correctly. Read it before starting Milestone 2 — it is the closest existing example of the pattern this plan generalizes.

### Why splitting into three roles is correct

The framework's view of the world is "an addressable slot (`IReference`) holds either canonical state (mutated by `Mutator`) or derived state (rebuilt by `IAggregateProvider`). The state is always immutable (`record` subclasses of `BaseState`)." For state-backed entities, the natural decomposition into that view is:

- The slot's identity is the entity's logical id — `InstanceId(int Id) : IReference`. Serializable, network-safe, lookup-friendly, value-typed.
- The canonical state (what mutators write) is the authored part: `EntityVariableState(BaseValues, ModifierStacks)`. Pure data, no derived caches.
- The derived state (what gameplay reads) is the rich snapshot: `StateEntity<TDef> : AggregateState`, holding everything a reader needs (id, definition, base values, modifier stacks, effective values), plus convenience methods. Always a record, always rebuilt from authored state.
- The behavior that produces the derived state is `EntityStateProvider<TDef> : AggregateProvider<StateEntity<TDef>>`. A class. The only place in the bridge that holds a definition reference at runtime; a fresh instance per entity created.

This decomposition trivially obeys the framework's invariants. The data is a record, the provider is a class, the reference is value-typed, and the bridge no longer needs auxiliary tables or singletons.


## Plan of Work

The work is three milestones plus a final validation gate. Each milestone leaves the code compilable and tested. Milestones 1 and 3 are independent improvements to `Scaffold.States`; Milestone 2 is the bridge refactor that depends on Milestone 1's `Store.RegisterAggregate`.


### Milestone 1 — `Store.RegisterAggregate` post-build

Goal: install an aggregate slice onto a `Store` after `Build()` has returned, so runtime-created entities can register their providers as aggregates.

Edit `Assets/Packages/com.scaffold.states/Runtime/Store.cs`. Add a new region "Aggregate registration" after the existing "Slice registration" region. Add the public method:

    public void RegisterAggregate(IReference reference, IAggregateProvider provider)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));
        var r = reference ?? Reference.Null;
        var aSlice = new AggregateSlice(r, provider);
        Type t = aSlice.StateType;
        ThrowIfSliceConflict(r, t, map.Contains(r, t), aggregates.Contains(r, t));
        aggregates.Add(r, t, aSlice);
        aSlice.OnAttachedToStore(this);
        eventHandler.Notify(r, aSlice.State, StateChangeEvent.Created);
    }

The `OnAttachedToStore` call is what wires the provider to source events and runs the initial `Build`. Calling `eventHandler.Notify(...)` after attach is cosmetic but matches the canonical-slice path's behavior of firing `Created` on registration.

Optionally also add a symmetric `UnregisterAggregate(IReference, Type)` method in the same shape as `UnregisterSlice`. Skip it for this milestone unless tests require it; the bridge does not unregister entities today.

Add a regression test at `Assets/Packages/com.scaffold.states/Tests/StoreRegisterAggregateTests.cs`. The test must demonstrate three things: aggregate registration after `Build()` succeeds and the aggregate is readable; subsequent canonical changes trigger a rebuild; `LoadSnapshot` after registration still triggers a rebuild. Keep the test self-contained — define small `TestSourceState`, `TestAggregateState`, and `TestProvider` types inside the test file.

Validate Milestone 1 by running `pwsh -NoProfile -File .agents\scripts\check-analyzers.ps1`. Expected: `BUILD_EXIT:0`, `TOTAL:0`. Then with Unity closed run `pwsh -NoProfile -File .agents\scripts\run-editmode-tests.ps1 -AssemblyNames Scaffold.States.Tests`. Expected: all existing tests plus the new ones pass.


### Milestone 2 — Bridge refactor: three roles, three types

Goal: refactor the bridge so that `InstanceId` is the framework reference, `StateEntity<TDef>` is the immutable record produced by the aggregate, and `EntityStateProvider<TDef>` is the dedicated provider class. Mutators write only authored data. The bridge's bookkeeping disappears.

This milestone touches multiple files and they only compile together. Make the changes in one branch, run validation at the end. The order below is the order to type the edits in; the commit may be a single squashed change.

#### Step 2.1 — Define `StateEntity<TDef>` as the aggregate record

Replace the contents of `Assets/Packages/com.scaffold.entities.states/Runtime/StateEntity.cs` with a record declaration. The record extends `AggregateState`, carries the data needed for any read, and exposes convenience read methods so callers do not have to dig into raw dictionaries. The previous class — a subclass of `BaseEntityInstance<TDef>` — is retired.

    using System;
    using System.Collections.Generic;
    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public sealed record StateEntity<TDefinition>(
            InstanceId Id,
            TDefinition Definition,
            IReadOnlyDictionary<Variable, VariableValue> BaseValues,
            IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks,
            IReadOnlyDictionary<Variable, VariableValue> EffectiveValues) : AggregateState
            where TDefinition : IEntityDefinition
        {
            public T GetVariable<T>(Variable key)
            {
                if (!TryGetVariable<T>(key, out var value))
                {
                    throw new InvalidOperationException($"Variable '{key?.Key ?? "?"}' is not defined on this entity.");
                }
                return value;
            }

            public bool TryGetVariable<T>(Variable key, out T value)
            {
                value = default!;
                if (key == null) return false;
                if (EffectiveValues.TryGetValue(key, out var ev) && ev is IVariableValue<T> typedE) { value = typedE.Get(); return true; }
                if (BaseValues.TryGetValue(key, out var bv) && bv is IVariableValue<T> typedB) { value = typedB.Get(); return true; }
                if (Definition.TryGetDefaultValue(key, out var dv) && dv is IVariableValue<T> typedD) { value = typedD.Get(); return true; }
                return false;
            }
        }
    }

The record carries `Definition` so reads do not require an external lookup, and `Id` so callers can identify themselves to the store. The `EffectiveValues` field is the precomputed fold output produced by the provider; `BaseValues` and the definition-default fallback are kept as backup paths for variables with no modifiers or no overrides.

#### Step 2.2 — Define `EntityStateProvider<TDef>`

Create `Assets/Packages/com.scaffold.entities.states/Runtime/EntityStateProvider.cs`:

    using System.Collections.Generic;
    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        internal sealed class EntityStateProvider<TDefinition> : AggregateProvider<StateEntity<TDefinition>>
            where TDefinition : IEntityDefinition
        {
            public EntityStateProvider(InstanceId id, TDefinition definition)
            {
                this.id = id;
                this.definition = definition;
            }

            private readonly InstanceId id;
            private readonly TDefinition definition;

            public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
            {
                scope.Events.Subscribe<EntityVariableState>(id, (_, _, _) => rebuild.RequestRebuild());
            }

            protected override StateEntity<TDefinition> BuildCore(IStateScope scope)
            {
                var source = scope.Get<EntityVariableState>(id);
                var effective = source.ResolveEffectiveValues(definition);
                return new StateEntity<TDefinition>(id, definition, source.BaseValues, source.ModifierStacks, effective);
            }
        }
    }

The provider holds the `InstanceId` and `IEntityDefinition` references — the only behavior-rich object in the bridge. It is `internal` because gameplay code does not interact with it directly; the factory wires it into the store and from there the framework drives it.

The fold itself lives on `EntityVariableState.ResolveEffectiveValues` (Step 2.3) — the provider does not duplicate it. `BuildCore` is purely orchestration: read the source slice, ask it to resolve effective values for the given definition, package the resulting dictionary alongside the rest of the data into a `StateEntity` record.

#### Step 2.3 — Reshape `EntityVariableState`: shrink fields, add behavior methods

Edit `Assets/Packages/com.scaffold.entities.states/Runtime/EntityVariableState.cs`. Two changes happen together: drop the `EffectiveValues` parameter and field (it now lives on the aggregate, not the source), and add behavior methods so mutators do not have to write out the bookkeeping long-form.

The record's data shape becomes:

    public sealed record EntityVariableState(
        IReadOnlyDictionary<Variable, VariableValue> BaseValues,
        IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks) : State

The static `Empty` updates to two empty dictionaries. The `CreateMutableValues` and `CreateMutableStacks` static helpers stay as private internals to support the new methods — they are no longer called from outside the record.

Add the following instance methods to the record. Each returns a new `EntityVariableState` reflecting the change; the receiver record is never mutated.

    public EntityVariableState WithModifier(Variable variable, ActiveModifier modifier);
    public EntityVariableState WithoutModifier(Variable variable, ModifierId modifierId);
    public EntityVariableState WithBaseValue(Variable variable, VariableValue value);
    public EntityVariableState WithVariable(Variable variable, VariableValue initialValue);

`WithModifier` copy-on-writes the `ModifierStacks` dictionary, copy-on-writes the bucket for the named variable (or creates a new empty bucket), inserts the new `ActiveModifier` at the correct index using ordered insertion (sorted by `Modifier.Order` ascending, ties broken by insertion order), and returns the record `with { ModifierStacks = nextStacks }`.

`WithoutModifier` copy-on-writes the stacks dictionary, copy-on-writes the bucket, removes the entry whose `Id.Equals(modifierId)`, and either removes the variable from the dictionary if the bucket is empty or replaces it. If the variable is not present or the modifier id is not found, returns `this` unchanged.

`WithBaseValue` copy-on-writes the `BaseValues` dictionary, sets the new value (overwriting any existing entry for the same variable), and returns the record `with { BaseValues = nextBases }`.

`WithVariable` is the additive form: only sets a `BaseValues` entry if the variable is not already present in `BaseValues`. Returns `this` unchanged if `BaseValues.ContainsKey(variable)` is already true. This method does not consult any `IEntityDefinition` — the definition-default check is intentionally not part of the record's contract; see Step 2.4 for the rationale.

Add one more method, the fold that produces effective values:

    public IReadOnlyDictionary<Variable, VariableValue> ResolveEffectiveValues(IEntityDefinition definition);

This iterates every key that appears in `BaseValues`, `ModifierStacks`, or `definition.DefinedVariables`. For each key: resolve the base value (override → `BaseValues`; otherwise `definition.TryGetDefaultValue`); if a non-empty modifier bucket exists for that key, call `baseValue.ApplyModifiers(bucket)` to fold; place the result in the output dictionary. Variables that have neither an override nor a default and have no modifiers do not appear in the output. Return the dictionary.

`ResolveEffectiveValues` is the same logic that `EffectiveValueRecomputer.RecomputeFor` did per variable, generalized to compute the entire effective dictionary in one pass. The provider calls it once per rebuild.

After this step, the record owns four "what does it mean to change me" methods plus one "how do I derive my effective view given a definition" method. Mutators in Step 2.4 become one-line translations from payload to method call.

#### Step 2.4 — Stateless one-line mutators

Edit each of the four mutators in `Assets/Packages/com.scaffold.entities.states/Runtime/Mutators/`. Remove the `EntityBridgeContext context` field and constructor parameter. With Step 2.3's behavior methods in place, each mutator's `Change` becomes a one-line translation from payload to record method call:

    // AddModifierMutator
    public override EntityVariableState Change(EntityVariableState state, AddModifierPayload payload, IStateScope scope)
        => state.WithModifier(payload.Variable, new ActiveModifier(payload.ModifierId, payload.Modifier));

    // RemoveModifierMutator
    public override EntityVariableState Change(EntityVariableState state, RemoveModifierPayload payload, IStateScope scope)
        => state.WithoutModifier(payload.Variable, payload.ModifierId);

    // SetBaseValueMutator
    public override EntityVariableState Change(EntityVariableState state, SetBaseValuePayload payload, IStateScope scope)
        => state.WithBaseValue(payload.Variable, payload.Value);

    // AddEntityVariableMutator
    // Note: this one needs the definition to honor the "do not add if already defined" rule.
    // Either embed the lookup on the record (see Step 2.3 — WithVariable takes IEntityDefinition),
    // or delegate to scope.Get<StateEntity<...>>() to read the entity's current definition.
    // The cleanest is the record method; mutators stay one-liners only when the record carries the rule.

The bookkeeping (copy-on-write of dictionaries, bucket cloning, ordered insertion, equality lookup for removal) lives on the record; mutators no longer need it. Mutators stay `internal sealed` and have parameterless constructors. None of them touch `EffectiveValues` (which no longer exists on `EntityVariableState`); the aggregate rebuild handles the derived view.

The `AddEntityVariableMutator` case has a subtle wrinkle: its rule is "do not add this variable if it is already known to the definition." That requires the definition. Two options for keeping the mutator a one-liner:
1. The record's `WithVariable(Variable, VariableValue, IEntityDefinition)` accepts the definition (Step 2.3). The mutator reads the definition from… where? It cannot easily — the mutator is stateless and has no entity reference. So this option requires the payload to carry the definition, which is awkward.
2. The record's `WithVariable(Variable, VariableValue)` has no definition awareness; it just adds the variable to `BaseValues` if not already present in `BaseValues`. The "do not duplicate definition default" check moves to the factory or to the caller writing the payload. The mutator stays trivially one-line.

Choose option 2. The simpler rule (no clobbering an existing `BaseValues` entry) is what the mutator actually enforces; the broader "respect definition defaults" was always somewhat redundant since `EntityVariableState` doesn't snapshot defaults anyway, and it kept `WithVariable` honest as a pure record method that does not need to know about definitions. Update the `WithVariable` method signature on `EntityVariableState` (Step 2.3) accordingly: drop the definition parameter; the method only checks `BaseValues.ContainsKey`.

#### Step 2.5 — Delete `EffectiveValueRecomputer`

Delete `Assets/Packages/com.scaffold.entities.states/Runtime/Mutators/EffectiveValueRecomputer.cs` and its `.meta` file. The fold logic now lives on `EntityVariableState.ResolveEffectiveValues` (Step 2.3), called once per rebuild from `EntityStateProvider.BuildCore` (Step 2.2).

#### Step 2.6 — Delete `StoreVariableStorage`

Delete `Assets/Packages/com.scaffold.entities.states/Runtime/StoreVariableStorage.cs` and its `.meta` file. Per-variable subscription fan-out is no longer a bridge concern: callers that want it can subscribe to `store.Events.Subscribe<StateEntity<TDef>>(id, callback)` and filter inside the callback (or maintain a small adapter on top).

If a per-variable adapter is wanted in the future, it is straightforward to write — but it is not part of this plan.

#### Step 2.7 — Strip `EntityBridgeContext`

Replace the entire body of `Assets/Packages/com.scaffold.entities.states/Runtime/EntityBridgeContext.cs` with:

    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public static class EntityBridgeContext
        {
            public static void RegisterMutators(Store store)
            {
                if (store == null) throw new System.ArgumentNullException(nameof(store));
                store.RegisterMutator(new AddModifierMutator());
                store.RegisterMutator(new RemoveModifierMutator());
                store.RegisterMutator(new SetBaseValueMutator());
                store.RegisterMutator(new AddEntityVariableMutator());
            }
        }
    }

The class becomes `public static`. `RegisterMutators` is the only surface. The `ConditionalWeakTable`, the lock, the `Bind` and `TryGetDefinition` methods, and the per-store dictionary are deleted.

#### Step 2.8 — `EntityStateFactory.Create`

Edit `Assets/Packages/com.scaffold.entities.states/Runtime/EntityStateFactory.cs`. The factory no longer touches `EntityBridgeContext`. It registers the source slice, creates a provider, registers the provider as an aggregate, and returns the initial built `StateEntity<TDef>` record.

    public static StateEntity<TDefinition> Create<TDefinition>(TDefinition definition, Store store, InstanceId instanceId)
        where TDefinition : IEntityDefinition
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (store == null) throw new ArgumentNullException(nameof(store));
        if (instanceId == null) throw new ArgumentNullException(nameof(instanceId));

        store.RegisterSlice(instanceId, EntityVariableState.Empty);

        var provider = new EntityStateProvider<TDefinition>(instanceId, definition);
        store.RegisterAggregate(instanceId, provider);

        return store.Get<StateEntity<TDefinition>>(instanceId);
    }

The factory returns the *initial* `StateEntity<TDefinition>` snapshot. Callers can hold it for an immediate read but must re-fetch (`store.Get<StateEntity<TDefinition>>(instanceId)`) or subscribe to see updates after subsequent `store.Execute(...)` calls. The `Id` field on the returned record is the durable handle for those operations.

The registration ordering matters: the source slice must exist before the aggregate is attached, because `OnAttachedToStore` runs an initial `Build` that reads `scope.Get<EntityVariableState>(instanceId)`.

#### Step 2.9 — Update tests

Edit `Assets/Packages/com.scaffold.entities.states/Tests/StateEntityIntegrationTests.cs` to reflect the new shape:

- The setup helper `CreateEntity()` and the inline setup in `TwoEntities_*` tests must call `EntityBridgeContext.RegisterMutators(store)` exactly once after `var store = new StoreBuilder().Build();`.
- The factory now returns a `StateEntity<EntityDefinition>` *record*, not a class. Adjust the tuple type returned by `CreateEntity()` accordingly.
- Tests that previously called `entity.GetVariable<float>(hp)` against a live class continue to work because the record exposes the same method, but they now read the snapshot at the moment the record was obtained. After any `store.Execute(...)`, tests must re-fetch via `store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp)` or similar.
- Delete the `DuplicateMutatorRegistration_AppliesPayloadTwice` test. The behavior it verifies is replaced by `MutatorRegistryDeduplicationTests.RegisterTwice_ThrowsDuplicateMutatorRegistrationException` in `Scaffold.States.Tests` after Milestone 3.
- Add a new test `Aggregate_RebuildsAfterLoadSnapshot`:

      [Test]
      public void Aggregate_RebuildsAfterLoadSnapshot()
      {
          var (store, _, _, id) = CreateEntity();
          var snapshot = store.SaveSnapshot();

          store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
          Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(15f));

          store.LoadSnapshot(snapshot);
          Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(10f),
              "After LoadSnapshot the source slice is restored and the aggregate must rebuild to the original effective value.");
      }

The existing `Snapshot_RoundTripsModifierStack` test is retained for the same reason: it covers the same flow but expresses the assertion through the older entity-style `GetVariable<T>` API path. Both are useful as regression guards.

Verify no other test depends on the deleted `EffectiveValues` field of `EntityVariableState` or on the deleted `StateEntity` class methods (`Subscribe`, `Unsubscribe`, etc.). If any test does, rewrite it to subscribe via `store.Events.Subscribe<StateEntity<...>>(id, callback)` instead.

#### Validation for Milestone 2

- Analyzer: `pwsh -NoProfile -File .agents\scripts\check-analyzers.ps1` reports `BUILD_EXIT:0`, `TOTAL:0`.
- Tests (Unity closed): `pwsh -NoProfile -File .agents\scripts\run-editmode-tests.ps1 -AssemblyNames Scaffold.Entities.States.Tests`. Expected: 9 tests pass (8 originals minus the deleted dispatch test, plus the new aggregate-rebuild test).
- Source size check: `EntityBridgeContext.cs` is approximately 12 lines; `EffectiveValueRecomputer.cs` is deleted; `StoreVariableStorage.cs` is deleted; `EntityVariableState.cs` no longer mentions `EffectiveValues`; `StateEntity.cs` is a record that extends `AggregateState`.


### Milestone 3 — `MutatorRegistry` dedupe

Goal: prevent the original dispatch foot-gun in any future `Scaffold.States` consumer. The bridge no longer triggers it (Path 2 only registers each mutator type once), but the protection is worth installing for downstream code.

Add a new exception type at `Assets/Packages/com.scaffold.states/Runtime/Pipeline/DuplicateMutatorRegistrationException.cs`:

    using System;

    namespace Scaffold.States
    {
        public sealed class DuplicateMutatorRegistrationException : Exception
        {
            public DuplicateMutatorRegistrationException(string message) : base(message) { }
        }
    }

The exception lives in its own file to satisfy the analyzer rule against multiple types per file.

Edit `Assets/Packages/com.scaffold.states/Runtime/Pipeline/MutatorRegistry.cs`. Modify `Register<TState, TPayload>` to walk the existing binding list and throw if any binding's runtime mutator type matches `mutator.GetType()`. The binding type may need to expose a `MutatorType` property; add it as `internal Type MutatorType => typeof(TState).IsValueType ? mutator.GetType() : mutator.GetType();` — a single `mutator.GetType()` reading is enough.

Sketch:

    public void Register<TState, TPayload>(Mutator<TState, TPayload> mutator) where TState : State
    {
        if (mutator is null) throw new ArgumentNullException(nameof(mutator));
        var key = typeof(TPayload);
        if (registrations.TryGetValue(key, out var list))
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is RegisteredMutator<TState, TPayload> rm && rm.MutatorType == mutator.GetType())
                {
                    throw new DuplicateMutatorRegistrationException(
                        $"A mutator of type {mutator.GetType().FullName} is already registered for payload {typeof(TPayload).FullName}.");
                }
            }
        }
        else
        {
            list = new List<IPayloadMutatorBinding>();
            registrations[key] = list;
        }
        list.Add(new RegisteredMutator<TState, TPayload>(mutator));
    }

Add the regression test `Assets/Packages/com.scaffold.states/Tests/MutatorRegistryDeduplicationTests.cs`:

    using NUnit.Framework;

    namespace Scaffold.States.Tests
    {
        public sealed class MutatorRegistryDeduplicationTests
        {
            private sealed record TestState(int Value) : State;
            private sealed record TestPayload();
            private sealed class TestMutator : Mutator<TestState, TestPayload>
            {
                public override TestState Change(TestState state, TestPayload payload, IStateScope scope) => state;
            }

            [Test]
            public void RegisterTwice_ThrowsDuplicateMutatorRegistrationException()
            {
                var registry = new MutatorRegistry();
                registry.Register(new TestMutator());
                Assert.Throws<DuplicateMutatorRegistrationException>(() => registry.Register(new TestMutator()));
            }
        }
    }

#### Validation for Milestone 3

- Analyzer: `BUILD_EXIT:0`, `TOTAL:0`.
- Tests: `Scaffold.States.Tests` runs all tests including the new dedupe test. The bridge tests must continue to pass — Path 2's `EntityBridgeContext.RegisterMutators` only registers each mutator class once across a Store's lifetime (because `RegisterMutators` is called once at setup), so the dedupe throw never fires from the bridge's normal use.


### Final validation gate

Run from repository root `C:\Unity\Scaffold`:

- `pwsh -NoProfile -File .agents\scripts\check-analyzers.ps1` — expect `BUILD_EXIT:0`, `TOTAL:0`.
- With Unity closed: `pwsh -NoProfile -File .agents\scripts\run-editmode-tests.ps1 -AssemblyNames Scaffold.States.Tests`, then `pwsh -NoProfile -File .agents\scripts\run-editmode-tests.ps1 -AssemblyNames Scaffold.Entities.States.Tests`, then `pwsh -NoProfile -File .agents\scripts\run-editmode-tests.ps1 -AssemblyNames Scaffold.Entities.Tests`. All three suites green.
- Source diff against `main`: `EntityBridgeContext.cs` collapses by ~30 lines; `EffectiveValueRecomputer.cs` is gone; `StoreVariableStorage.cs` is gone; `Store.cs` gains ~15 lines for `RegisterAggregate`; `MutatorRegistry.cs` gains ~10 lines for the dedupe check; `StateEntity.cs` is a small record file; `EntityStateProvider.cs` is a new file.

Commit grouping: one commit per milestone is the natural unit. Milestones 1 and 3 are touch-points in `Scaffold.States` that could land on `main` independently of Milestone 2; the bridge refactor in Milestone 2 logically depends on Milestone 1 but not Milestone 3.


## Validation and Acceptance

The change is complete when **all** of the following hold:

1. `Assets/Packages/com.scaffold.states/Runtime/Store.cs` exposes a public `RegisterAggregate(IReference, IAggregateProvider)` method, exercised by `StoreRegisterAggregateTests` covering the create / rebuild / snapshot-load paths.
2. `Assets/Packages/com.scaffold.entities.states/Runtime/EntityBridgeContext.cs` is approximately 12 lines and contains a single `public static void RegisterMutators(Store store)` method. No static fields, no locks, no maps, no `ConditionalWeakTable`, no instance state.
3. `EntityVariableState` is `(BaseValues, ModifierStacks)` and exposes `WithModifier`, `WithoutModifier`, `WithBaseValue`, `WithVariable`, and `ResolveEffectiveValues(IEntityDefinition)` as instance methods. `StateEntity<TDef> : AggregateState` is a record with `(Id, Definition, BaseValues, ModifierStacks, EffectiveValues)` and convenience read methods. `EntityStateProvider<TDef> : AggregateProvider<StateEntity<TDef>>` is the dedicated provider class. `EffectiveValueRecomputer.cs` and `StoreVariableStorage.cs` are deleted.
4. The four mutators have parameterless constructors. Each mutator's `Change` body is a single expression delegating to a record method on `EntityVariableState` — no manual dictionary copying or fold logic in the mutator.
5. `EntityStateFactory.Create` registers the source slice and the aggregate provider via `Store.RegisterSlice` and `Store.RegisterAggregate`. It does not register mutators. Setup code calls `EntityBridgeContext.RegisterMutators(store)` once before any `Create`. The factory returns the initial `StateEntity<TDef>` record (re-fetch via `store.Get<StateEntity<TDef>>(id)` for later snapshots).
6. `MutatorRegistry.Register` throws `DuplicateMutatorRegistrationException` when called twice with the same `(TState, TPayload, mutator concrete runtime type)`. Demonstrated by `MutatorRegistryDeduplicationTests.RegisterTwice_ThrowsDuplicateMutatorRegistrationException`.
7. The 8 surviving bridge integration tests plus the new `Aggregate_RebuildsAfterLoadSnapshot` test pass; entities and states test suites also green; analyzer reports zero issues.

The change is **not** complete if any of these fail. Failed tests are debugged and root-caused, not deleted. The retired `DuplicateMutatorRegistration_AppliesPayloadTwice` test is the one exception — it is replaced, not deleted to hide a regression.


## Idempotence and Recovery

Each milestone ends in a compilable, committable state. If work is interrupted:

- After Milestone 1, the `Store.RegisterAggregate` method is harmless if not yet used by any consumer. The bridge and its tests continue to work as today.
- During Milestone 2, the bridge package will not compile between intermediate steps (the source slice and mutator changes are intertwined, and the new `StateEntity` record replaces the old `StateEntity` class). Plan to land Milestone 2 as one squashed commit. If the work is paused mid-milestone, `git stash` or branch off a checkpoint commit; do not push a partial Milestone 2 to a shared branch.
- After Milestone 2, Milestone 3 is independent: the registry dedupe does not change behavior for code that already registers each mutator class once, which is the only way Path 2 ever uses the registry.

Re-running every step is safe: file edits are idempotent, asmdef edits are idempotent, and tests are pure.

If `OnAttachedToStore` runs `Build` before the source slice exists (a registration ordering bug in `EntityStateFactory.Create`), `scope.Get<EntityVariableState>(instanceId)` will throw `KeyNotFoundException`. Fix by registering the source slice before the aggregate. The factory body in Step 2.8 enforces this order.

If `MutatorRegistry`'s dedupe throw fires from the bridge's normal usage during Milestone 3 testing, that is a sign Path 2 is registering some mutator more than once — probably because `EntityBridgeContext.RegisterMutators` is being called more than once on the same store. Audit the test setup; the helper is meant to be called once per store.


## Artifacts and Notes

Expected `EntityBridgeContext.cs` after Milestone 2 (full file, approximately 12 lines):

    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public static class EntityBridgeContext
        {
            public static void RegisterMutators(Store store)
            {
                if (store == null) throw new System.ArgumentNullException(nameof(store));
                store.RegisterMutator(new AddModifierMutator());
                store.RegisterMutator(new RemoveModifierMutator());
                store.RegisterMutator(new SetBaseValueMutator());
                store.RegisterMutator(new AddEntityVariableMutator());
            }
        }
    }

Expected `StateEntity.cs` shape (full file structure, body of methods elided for brevity):

    public sealed record StateEntity<TDefinition>(
        InstanceId Id,
        TDefinition Definition,
        IReadOnlyDictionary<Variable, VariableValue> BaseValues,
        IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks,
        IReadOnlyDictionary<Variable, VariableValue> EffectiveValues) : AggregateState
        where TDefinition : IEntityDefinition
    {
        public T GetVariable<T>(Variable key);
        public bool TryGetVariable<T>(Variable key, out T value);
    }

Expected `EntityStateProvider.cs` shape:

    internal sealed class EntityStateProvider<TDefinition> : AggregateProvider<StateEntity<TDefinition>>
        where TDefinition : IEntityDefinition
    {
        public EntityStateProvider(InstanceId id, TDefinition definition);
        public override void Wire(IStoreScope scope, IAggregateRebuild rebuild);
        protected override StateEntity<TDefinition> BuildCore(IStateScope scope);
    }

Expected `EntityVariableState.cs` shape (full structure, method bodies elided):

    public sealed record EntityVariableState(
        IReadOnlyDictionary<Variable, VariableValue> BaseValues,
        IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks) : State
    {
        public static EntityVariableState Empty { get; }

        public EntityVariableState WithModifier(Variable variable, ActiveModifier modifier);
        public EntityVariableState WithoutModifier(Variable variable, ModifierId modifierId);
        public EntityVariableState WithBaseValue(Variable variable, VariableValue value);
        public EntityVariableState WithVariable(Variable variable, VariableValue initialValue);
        public IReadOnlyDictionary<Variable, VariableValue> ResolveEffectiveValues(IEntityDefinition definition);
    }

Expected mutator shape (full body for `AddModifierMutator`; the others follow the same one-line pattern):

    internal sealed class AddModifierMutator : Mutator<EntityVariableState, AddModifierPayload>
    {
        public override EntityVariableState Change(EntityVariableState state, AddModifierPayload payload, IStateScope scope)
            => state.WithModifier(payload.Variable, new ActiveModifier(payload.ModifierId, payload.Modifier));
    }

Expected `Store.RegisterAggregate` signature added to `Assets/Packages/com.scaffold.states/Runtime/Store.cs`:

    public void RegisterAggregate(IReference reference, IAggregateProvider provider);

Expected snapshot of bridge usage at the call site (test setup):

    var store = new StoreBuilder().Build();
    EntityBridgeContext.RegisterMutators(store);
    var heroId = new InstanceId(1);
    var goblinId = new InstanceId(2);
    var hero = EntityStateFactory.Create(heroDef, store, heroId);          // initial StateEntity<HeroDef> record
    var goblin = EntityStateFactory.Create(goblinDef, store, goblinId);

    store.Execute(heroId, new AddModifierPayload(heroId, hp, new FloatAddModifier(5f), ModifierId.New()));

    var heroNow = store.Get<StateEntity<HeroDef>>(heroId);                 // re-fetch for the latest snapshot
    Assert.That(heroNow.GetVariable<float>(hp), Is.EqualTo(15f));

    using var sub = store.Events.Subscribe<StateEntity<HeroDef>>(heroId, (id, e, evt) => { /* react */ });


## Revision history

- **2026-04-29** — Initial ExecPlan authored after research into `Scaffold.States.AggregateSlice`, `IAggregateProvider`, and the snapshot/scratchpad interaction. Goal: replace the bridge's bespoke effective-value cache + per-store definition map with the framework's existing aggregate primitive. Supersedes `Plans/StatelessEntityBridge/StatelessEntityBridge-ExecPlan.md`. Author: design conversation, 2026-04-29.
- **2026-04-29 (revision)** — Restructured to split state-backed entities into three explicit roles: `InstanceId : IReference` (identity), `StateEntity<TDef> : AggregateState` (the immutable record produced by the aggregate, with convenience read methods), and `EntityStateProvider<TDef> : AggregateProvider<StateEntity<TDef>>` (the dedicated provider class). Earlier draft conflated identity, behavior, and data into one type. Also clarifies that `InstanceId` stays as the durable handle for networking and lookups, and that gameplay reads become snapshot-oriented (`store.Get<StateEntity<TDef>>(id)` or subscriptions) rather than always-fresh through a live class wrapper. Author: design conversation, 2026-04-29.
- **2026-04-29 (revision 2)** — Pushed bookkeeping behavior onto `EntityVariableState` itself. The record now carries `WithModifier`, `WithoutModifier`, `WithBaseValue`, `WithVariable`, and `ResolveEffectiveValues(IEntityDefinition)` as instance methods that return new records; mutators collapse to one-line translations from payload to method call. Confirmed `StoreVariableStorage` is deleted (its job is subsumed by aggregate subscriptions); confirmed `EntityBridgeContext` stays a static helper rather than a `Store` subclass. Author: design conversation, 2026-04-29.
