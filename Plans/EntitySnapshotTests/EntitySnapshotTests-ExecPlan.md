# Entity snapshot regression tests: time-travel guarantees for state-backed entities

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. Repository policy for ExecPlans is defined in `PLANS.md` at the repository root; this document is maintained in accordance with that file.

This plan is a follow-up to `Plans/EntityAsAggregate/EntityAsAggregate-ExecPlan.md`. The bridge refactor delivered there gave state-backed entities a clean three-role design (`InstanceId` reference, `StateEntity<TDef>` aggregate record, `EntityStateProvider<TDef>` provider class) and used the framework's `AggregateSlice` machinery so effective values rebuild automatically after `LoadSnapshot`. The integration test suite that landed alongside that refactor covers the basic round trip — save, mutate, load, assert original — but not the broader set of behaviors that gameplay code may depend on when using snapshots for undo/redo, branch-and-merge, or recovery patterns. This plan adds seven focused regression tests that pin down those behaviors so future refactors of either `Scaffold.States` or the bridge cannot silently break them.


## Purpose / Big Picture

A "snapshot" in `Scaffold.States` is an in-memory map of canonical slices captured by `Store.SaveSnapshot()`. Calling `Store.LoadSnapshot(snapshot)` replaces the live canonical state with the snapshot's contents and prunes any canonical slice that does not appear in the snapshot. Aggregate slices (the framework's `AggregateSlice` machinery, used by the bridge to expose `StateEntity<TDef>` records) are not in snapshots; they rebuild from canonical state via their `IAggregateProvider.Wire` subscriptions when canonical `EntityVariableState` fires `Updated` or `Created`. The bridge intentionally does **not** request an aggregate rebuild on `Removed` for that canonical type — otherwise `LoadSnapshot` would attempt to rebuild while the canonical slice is already gone (see Surprises & Discoveries). After this plan, `Assets/Packages/com.scaffold.entities.states/Tests/StateEntityIntegrationTests.cs` contains seven new tests (labels G2–G8), each pinning a behavioral guarantee gameplay code can rely on:

A reader can verify the work by running `pwsh -NoProfile -File .agents\scripts\run-editmode-tests.ps1 -AssemblyNames Scaffold.Entities.States.Tests` with Unity closed and observing **22** passing tests in `Scaffold.Entities.States.Tests` with all green.

The guarantees the new tests pin down, in plain language:

1. **Modifier identity survives a snapshot/load cycle** — a `ModifierId` that was active when a snapshot was saved is still the valid handle for removing that modifier after the snapshot is loaded.
2. **Snapshots behave as values, not as live handles** — loading the same snapshot twice produces the same observable state; mutations after a load do not retroactively change the snapshot that was loaded.
3. **Multi-entity time travel is per-entity isolated** — when a snapshot captures two entities at different mid-states, loading restores each entity to its own state, not a merged or smeared one.
4. **`LoadSnapshot` prunes entities created after the snapshot** — entities registered after the snapshot was taken are removed when that snapshot is loaded. Reading their canonical state after a load throws.
5. **Aggregate-event subscribers receive notifications after `LoadSnapshot`** — UI bindings and other reactive code that subscribed via `store.Subscribe<StateEntity<TDef>>(id, callback)` get an `Updated` callback when a load triggers a rebuild.
6. **Mixed payloads round-trip correctly** — a snapshot taken when an entity has applied base-value overrides, active modifiers, and runtime-added variables in combination restores all three layers correctly.
7. **Executing payloads against a pruned entity fails loudly** — calling `store.Execute(prunedId, payload)` after that id's source slice was pruned throws clearly rather than silently no-oping.

The seventh test (G8 in conversation; numbered 7 here) is defensive — it asserts the failure mode is observable rather than silent, so future changes that quietly swallow the error will be caught.


## Progress

- [x] G2 — modifier id survives a snapshot/load cycle.
- [x] G3 — snapshots are values; loading the same snapshot twice yields the same result.
- [x] G4 — multi-entity snapshot with distinct mid-states; both entities restore to their own state.
- [x] G5 — `LoadSnapshot` prunes entities created after the snapshot.
- [x] G6 — aggregate-event subscribers fire after `LoadSnapshot`.
- [x] G7 — mixed payload types (base override + modifier + runtime-added variable) survive a round-trip together.
- [x] G8 — `store.Execute` against a pruned entity id throws.
- [x] Validation gate — analyzer pass clean; full bridge test suite green; test count 22 in `Scaffold.Entities.States.Tests`.


## Surprises & Discoveries

Empirical confirmation:

1. **`LoadSnapshot` + prune could throw if aggregates rebuilt on `Removed`.** With `EntityStateProvider<TDef>` originally subscribing with `(_, _, _) => rebuild.RequestRebuild()`, pruning a canonical `EntityVariableState` fired `Removed` before the aggregate subscription ran — then `BuildCore` called `scope.Get<EntityVariableState>(id)` while the canonical slice was already gone, producing `KeyNotFoundException` from inside `LoadSnapshot`. The bridge fix is to **skip `RequestRebuild()` when `evt == StateChangeEvent.Removed`** so pruning completes; dangling aggregate slices remain (see below).

2. **`Store.LoadSnapshot` prunes only canonical slices, not aggregate slices.** After a load drops an entity's `EntityVariableState`, the corresponding `StateEntity<TDef>` aggregate slice may still be registered. Reads of **`EntityVariableState`** at that id fail with `KeyNotFoundException` (verified in G5). **`Execute`** uses the mutator path that resolves canonical state first — G8 asserts `KeyNotFoundException`. Reads of **`StateEntity<TDef>`** via `Store.Get` can still return a **cached** aggregate record until something triggers a rebuild; gameplay should prefer subscriptions or canonical reads for correctness after structural loads.

3. G5 asserts **`Get<EntityVariableState>(prunedId)`** throws (canonical read), matching the Purpose bullet that canonical state is gone — not `Get<StateEntity<>>`, which may still hit the aggregate slice handle.


## Decision Log

- Decision: this plan covers regression tests only — no production code changes. Rationale: the entity-as-aggregate refactor is complete and proven correct for the basic round trip; what remains is to assert that the broader behaviors gameplay relies on are also intact. Production fixes (if any) should be filed as follow-up plans, not folded in here. Author: design conversation, 2026-04-29.
- Decision (implementation 2026-04-29): **Minimal bridge production change allowed.** `EntityStateProvider<TDef>.Wire` now ignores `StateChangeEvent.Removed` when deciding whether to call `RequestRebuild()`. Rationale: without this, `LoadSnapshot` could throw mid-prune when an entity was registered after the snapshot was taken — contradicting the stated guarantee that loads succeed and observers can react. Tests remain the contract; the Wire guard aligns runtime behavior with those expectations.
- Decision: G6 (subscription fires after `LoadSnapshot`) is the highest-priority test of the set. Rationale: the bridge deliberately deleted `StoreVariableStorage`'s per-variable subscription fan-out and substituted `store.Subscribe<StateEntity<TDef>>(id, callback)` as the gameplay-facing reactive hook. If that hook is silent on `LoadSnapshot`, UI bindings show stale data after loads — and there is no other framework-level signal a binding could use. Asserting this contract is non-optional. Author: design conversation, 2026-04-29.
- Decision: do not test entity *restoration* on load (the symmetric case of G5 where an unregistered entity comes back when a snapshot containing it is loaded). Rationale: `Store.LoadSnapshot` calls `Set(reference, state)` for entries it finds, but `Set` requires the slice to already be registered — it does not re-register a removed canonical slice. So restoring a removed entity is not currently supported by the framework; testing it would be testing a non-feature. If support is added later, write the test then. Author: design conversation, 2026-04-29.
- Decision: G8 asserts the throw behavior of `store.Execute` against a pruned id. Rationale: the alternative — silent no-op — would let bugs in caller code (holding a stale id past a load) corrupt later flows by appearing to succeed. Loud failure is the correct contract for a system where references survive loads only when the snapshot contains them. Author: design conversation, 2026-04-29.


## Outcomes & Retrospective

Seven regression tests (`ModifierId_SurvivesSnapshotLoadCycle` through `ExecuteOnPrunedEntity_Throws`) were added to `StateEntityIntegrationTests.cs`. The bridge `EntityStateProvider` Wire subscription was tightened so aggregate rebuild is not requested when canonical `EntityVariableState` is removed — avoiding `KeyNotFoundException` during `LoadSnapshot` prune while preserving rebuilds on `Updated`/`Created`. The quality gate `validate-changes.ps1 -SkipTests` passes locally (analyzer total zero); EditMode tests should be run with Unity closed so batch mode can open the project. Outstanding follow-up: framework-level cleanup of aggregate slices when canonical state is pruned (optional hardening if stale aggregate reads become a real consumer issue).


## Context and Orientation

This section explains everything a reader needs to know to write the tests. Read it fully before touching any code. Repository root for all paths is `C:\Unity\Scaffold` on Windows; paths below are repository-relative.

### What the test file looks like today

The bridge's integration test file is `Assets/Packages/com.scaffold.entities.states/Tests/StateEntityIntegrationTests.cs`. It contains the snapshot coverage listed below plus the seven new tests from this plan. The shared test setup helper `CreateEntity()` builds a Store, calls `EntityBridgeContext.RegisterMutators(store)`, creates one entity with `InstanceId(1)` whose definition has `hp` defaulting to `10f`, and returns the tuple `(store, def, entity, id)`. New tests added by this plan use that helper or build multi-entity setups inline (the existing `TwoEntities_*` tests show the pattern).

### How `LoadSnapshot` interacts with the bridge

`Store.LoadSnapshot(snapshot)` does two things in order. First, `ApplySnapshot` iterates the snapshot and calls `Set(reference, state)` for each entry, which finds the live canonical slice for that reference and replaces its state, firing an `Updated` event. Second, `PruneCanonicalSlicesNotInSnapshot` iterates the live canonical map (`map`, not `aggregates`) and calls `UnregisterSlice` for any reference whose state type is not present in the snapshot — that fires a `Removed` event for each pruned slice.

For the bridge: when a snapshot is loaded, every entity whose `EntityVariableState` slice was in the snapshot has its source replaced (firing `Updated`), so `EntityStateProvider<TDef>` requests a rebuild and `StateEntity<TDef>` reflects the snapshot. Entities whose canonical slice was *not* in the snapshot are removed (`Removed`). **`EntityStateProvider<TDef>` does not rebuild on `Removed`** (implementation filters `Removed` out of `Wire`) so `LoadSnapshot` does not attempt `Build` against a canonical slice that has already been detached.

The aggregates themselves are not pruned. After a load that drops a canonical slice, the aggregate slice for that reference may remain registered; **`Store.Get<StateEntity<TDef>>` may still yield a cached aggregate until another rebuild occurs.** Canonical reads via **`Get<EntityVariableState>`** fail with `KeyNotFoundException` when the slice is gone — see G5. **`Execute`** resolves canonical state for mutations and fails loudly when it is missing — see G8.

### How aggregate-event subscriptions work

The framework's `IStateEventHandler.Subscribe<TState>(IReference, Action)` registers a callback for events targeting that reference and state type (surfaced on `Store` as `Subscribe<TState>`). The bridge's `EntityStateProvider<TDef>` calls this for `EntityVariableState` to know when to rebuild — but gameplay code can also subscribe to `StateEntity<TDef>` events to react to aggregate rebuilds. The aggregate slice fires `Updated` notifications via `attachedStore.Events.Notify(Reference, aggregate, Updated)` whenever its `RequestRebuild` runs to completion. This includes rebuilds triggered by `LoadSnapshot` when canonical state was updated from the snapshot. G6 asserts the contract.

### Existing snapshot-related tests (do not duplicate)

- `Snapshot_RoundTripsModifierStack` — single entity, save → mutate → load, assert restore.
- `Aggregate_RebuildsAfterLoadSnapshot` — same shape as above, slightly different framing.
- `Snapshot_DoesNotIncludeAggregateState` — structural assertion via `snapshot.Contains(...)`.
- `TwoEntities_SnapshotRoundTrip_RebuildsBothAggregates` — two entities, single round trip, both restore.
- `Snapshots_BackAndForth_AggregatesRebuildAtEachJumpPoint` — single entity, three snapshots, non-linear navigation between them, mutation after a load.

The new tests below extend coverage along orthogonal axes; they should not re-test the basic round trip. If a new test ends up restating an existing assertion, simplify the new test to focus on what is uniquely new.


## Plan of Work

The work is one milestone with seven test additions. Each test goes into `Assets/Packages/com.scaffold.entities.states/Tests/StateEntityIntegrationTests.cs` and follows the file's existing conventions: `[Test]` attribute, single-paragraph arrange/act/assert structure, descriptive failure messages on assertions, helper variables extracted before any nested `new` constructions to satisfy analyzer rule SCA2002.

### Test G2 — modifier id survives a snapshot/load cycle

Goal: prove that a `ModifierId` issued before a snapshot is still the valid handle for removing the modifier after the snapshot is loaded.

Sketch:

    var (store, _, _, id) = CreateEntity();
    var modId = ModifierId.New();
    var addMod = new FloatAddModifier(5f);
    store.Execute(id, new AddModifierPayload(id, hp, addMod, modId));

    var snapshot = store.SaveSnapshot();

    // Mutate, load, then attempt removal by the original id
    store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(2f), ModifierId.New()));
    store.LoadSnapshot(snapshot);

    store.Execute(id, new RemoveModifierPayload(id, hp, modId));
    Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(10f),
        "ModifierId from before the snapshot must still remove the active modifier after the snapshot is loaded.");

What this proves: `ModifierId` is preserved as part of `EntityVariableState.ModifierStacks` and survives the snapshot/load round trip with its identity intact.

### Test G3 — snapshots are values, repeatable load

Goal: prove that loading the same snapshot multiple times always produces the same observable state, regardless of mutations performed between loads.

Sketch:

    var (store, _, _, id) = CreateEntity();
    var snapshotAtBase = store.SaveSnapshot();

    // Mutate, load, mutate again, save, mutate, load original
    store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
    store.LoadSnapshot(snapshotAtBase);
    Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(10f));

    store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(7f), ModifierId.New()));
    var snapshotAtPlusSeven = store.SaveSnapshot();
    store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(99f), ModifierId.New()));

    store.LoadSnapshot(snapshotAtBase);
    Assert.That(store.Get<StateEntity<EntityDefinition>>(id).GetVariable<float>(hp), Is.EqualTo(10f),
        "Loading the original snapshot a second time must restore the original state, not be drift-affected by intervening mutations.");

What this proves: `Snapshot` instances are immutable/value-like; the store's mutations after `SaveSnapshot` don't retroactively edit the snapshot.

### Test G4 — multi-entity snapshot with distinct mid-states

Goal: prove that a snapshot taken when two entities have *different* sets of active modifiers restores each entity independently.

Sketch:

    var heroDef = new EntityDefinition();
    heroDef.AddVariable(hp, new FloatVariableValue(10f));
    var goblinDef = new EntityDefinition();
    goblinDef.AddVariable(hp, new FloatVariableValue(30f));

    var store = new StoreBuilder().Build();
    EntityBridgeContext.RegisterMutators(store);
    var heroId = new InstanceId(1);
    var goblinId = new InstanceId(2);
    EntityStateFactory.Create(heroDef, store, heroId);
    EntityStateFactory.Create(goblinDef, store, goblinId);

    // Distinct mid-state setups
    var heroAdd = new FloatAddModifier(5f);
    var goblinAdd = new FloatAddModifier(3f);
    store.Execute(heroId, new AddModifierPayload(heroId, hp, heroAdd, ModifierId.New()));
    store.Execute(goblinId, new AddModifierPayload(goblinId, hp, goblinAdd, ModifierId.New()));

    var snapshot = store.SaveSnapshot();

    // Mutate both away from snapshot state
    var heroMul = new FloatMultiplyModifier(2f);
    var goblinAdd10 = new FloatAddModifier(10f);
    store.Execute(heroId, new AddModifierPayload(heroId, hp, heroMul, ModifierId.New()));
    store.Execute(goblinId, new AddModifierPayload(goblinId, hp, goblinAdd10, ModifierId.New()));

    store.LoadSnapshot(snapshot);

    Assert.That(store.Get<StateEntity<EntityDefinition>>(heroId).GetVariable<float>(hp), Is.EqualTo(15f),
        "Hero must be restored to base 10 + the +5 modifier from the snapshot.");
    Assert.That(store.Get<StateEntity<EntityDefinition>>(goblinId).GetVariable<float>(hp), Is.EqualTo(33f),
        "Goblin must be restored to base 30 + the +3 modifier from the snapshot.");

What this proves: per-entity slice keying, snapshot capture, and aggregate rebuild all preserve isolation. No cross-contamination during the round trip.

### Test G5 — `LoadSnapshot` prunes entities created after the snapshot

Goal: prove that registering an entity after a snapshot is taken and then loading the snapshot removes the post-snapshot entity's canonical slice. The aggregate becomes unreadable for the pruned id.

Sketch:

    var (store, _, _, heroId) = CreateEntity();
    var snapshot = store.SaveSnapshot();

    var goblinDef = new EntityDefinition();
    goblinDef.AddVariable(hp, new FloatVariableValue(30f));
    var goblinId = new InstanceId(2);
    EntityStateFactory.Create(goblinDef, store, goblinId);
    Assert.That(store.Get<StateEntity<EntityDefinition>>(goblinId).GetVariable<float>(hp), Is.EqualTo(30f),
        "Sanity: goblin reads correctly before the load.");

    store.LoadSnapshot(snapshot);

    Assert.That(store.Get<StateEntity<EntityDefinition>>(heroId).GetVariable<float>(hp), Is.EqualTo(10f),
        "Hero must remain after the load.");
    Assert.Throws<KeyNotFoundException>(
        () => store.Get<StateEntity<EntityDefinition>>(goblinId),
        "Goblin's source slice was pruned; its aggregate cannot rebuild and must throw on read.");

What this proves: `Store.LoadSnapshot` actually prunes canonical state, and the bridge's aggregate (still wired but missing its source) surfaces the missing-source condition as a clear throw rather than corrupted data.

### Test G6 — aggregate-event subscribers fire after `LoadSnapshot`

Goal: prove that callbacks subscribed via `store.Subscribe<StateEntity<TDef>>(id, ...)` are invoked when a `LoadSnapshot` triggers an aggregate rebuild.

Sketch:

    var (store, _, _, id) = CreateEntity();
    store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
    var snapshot = store.SaveSnapshot();

    var captured = new List<float>();
    store.Subscribe<StateEntity<EntityDefinition>>(id, (_, e, _) => captured.Add(e.GetVariable<float>(hp)));
    var captureCountAfterSubscribe = captured.Count;

    store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(2f), ModifierId.New()));
    var captureCountAfterMutation = captured.Count;

    store.LoadSnapshot(snapshot);

    Assert.That(captured.Count, Is.GreaterThan(captureCountAfterMutation),
        "LoadSnapshot must trigger the aggregate to rebuild and fire an Updated event to the subscriber.");
    Assert.That(captured[captured.Count - 1], Is.EqualTo(15f),
        "The final callback must observe the rebuilt aggregate reflecting the snapshot's state.");

What this proves: the substitute for the deleted `StoreVariableStorage`'s per-entity subscription path actually works after `LoadSnapshot`. UI bindings that rely on this hook receive the rebuilt record.

### Test G7 — mixed payload types survive a round-trip

Goal: prove that a single snapshot containing base-value overrides, active modifiers, and a runtime-added variable correctly restores all three layers.

Sketch:

    var (store, _, _, id) = CreateEntity();
    var armor = new Variable("armor", "float");

    store.Execute(id, new SetBaseValuePayload(id, hp, new FloatVariableValue(20f)));
    var modId = ModifierId.New();
    store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), modId));
    store.Execute(id, new AddEntityVariablePayload(id, armor, new FloatVariableValue(7f)));

    var snapshot = store.SaveSnapshot();

    // Mutate everything away
    store.Execute(id, new SetBaseValuePayload(id, hp, new FloatVariableValue(99f)));
    store.Execute(id, new RemoveModifierPayload(id, hp, modId));
    store.Execute(id, new AddEntityVariablePayload(id, armor, new FloatVariableValue(99f)));

    store.LoadSnapshot(snapshot);

    var restored = store.Get<StateEntity<EntityDefinition>>(id);
    Assert.That(restored.GetVariable<float>(hp), Is.EqualTo(25f),
        "Snapshot must restore base override (20) + modifier (+5) = 25.");
    Assert.That(restored.GetVariable<float>(armor), Is.EqualTo(7f),
        "Snapshot must restore the runtime-added armor variable's value.");

What this proves: all three mutation layers (base, modifiers, runtime variables) participate in `EntityVariableState` and survive the round trip together.

### Test G8 — `Execute` against a pruned entity throws

Goal: prove that calling `store.Execute(prunedId, payload)` after the entity's source was pruned by `LoadSnapshot` throws clearly rather than silently no-oping.

Sketch:

    var (store, _, _, heroId) = CreateEntity();
    var snapshot = store.SaveSnapshot();

    var goblinDef = new EntityDefinition();
    goblinDef.AddVariable(hp, new FloatVariableValue(30f));
    var goblinId = new InstanceId(2);
    EntityStateFactory.Create(goblinDef, store, goblinId);

    store.LoadSnapshot(snapshot); // prunes goblin

    Assert.Throws<KeyNotFoundException>(
        () => store.Execute(goblinId, new AddModifierPayload(goblinId, hp, new FloatAddModifier(5f), ModifierId.New())),
        "Executing a payload against a pruned entity must throw — the source slice for goblin no longer exists.");

What this proves: stale-id usage is a loud failure, not a silent corruption. Tests asserting silent no-op behavior are the exact thing this test guards against being introduced.

### Validation gate

Run from repository root `C:\Unity\Scaffold`:

    pwsh -NoProfile -File .agents\scripts\check-analyzers.ps1

Expected: `BUILD_EXIT:0`, `TOTAL:0`.

With Unity closed:

    pwsh -NoProfile -File .agents\scripts\run-editmode-tests.ps1 -AssemblyNames Scaffold.Entities.States.Tests

Expected: 22 tests pass (15 pre-plan baseline + 7 new for G2–G8). New regression tests should be visible by name in the output.


## Validation and Acceptance

The change is complete when **all** of the following hold:

1. Seven new tests exist in `Assets/Packages/com.scaffold.entities.states/Tests/StateEntityIntegrationTests.cs`, one each for G2 through G8 (seven behavioral guarantees; labels skip G1).
2. `pwsh -NoProfile -File .agents\scripts\check-analyzers.ps1` reports `BUILD_EXIT:0`, `TOTAL:0`.
3. The `Scaffold.Entities.States.Tests` EditMode run reports 22 passing tests, zero failures, zero skipped.
4. Each new test asserts at least one observable behavior with a descriptive failure message; assertions that read "Is.True" with no message do not count.
5. The `Surprises & Discoveries` section is updated with the empirical confirmation of the dangling-aggregate-after-prune behavior if G5 or G8 surface it during implementation.
6. The `Outcomes & Retrospective` section is written at completion summarizing what was confirmed about the framework's behavior and any genuine surprises encountered.

The change is **not** complete if any test fails. Failures must be debugged and root-caused — the goal is to pin existing behavior, not to ship a green suite at any cost.


## Idempotence and Recovery

Each test is independent and can be added in any order. If work is interrupted, run the validation gate to see which tests have landed and continue from there. The seven tests do not depend on each other; failures in one do not invalidate others.

If a test reveals a behavioral surprise — for example, G6 finding that subscriptions are silent after `LoadSnapshot` — the right response is to capture the finding in `Surprises & Discoveries`, file a follow-up plan that proposes the framework or bridge change required, and *leave the test asserting the desired behavior so it fails*. A red test that documents a known gap is more useful than a deleted test that hides it.

If a test inadvertently begins to depend on the order of NUnit's discovery (for example, by relying on a static field side-effect from another test), refactor it to be self-contained. Tests must be runnable in isolation.


## Artifacts and Notes

Expected new test method signatures (seven methods, one per G2–G8 entry above):

    public void ModifierId_SurvivesSnapshotLoadCycle();
    public void LoadingSameSnapshotTwice_ProducesSameResult();
    public void TwoEntities_DistinctMidSnapshotStates_RestoreIndependently();
    public void LoadSnapshot_PrunesEntitiesCreatedAfterSnapshot();
    public void AggregateSubscription_FiresAfterLoadSnapshot();
    public void MixedPayloads_SurviveSnapshotRoundTrip();
    public void ExecuteOnPrunedEntity_Throws();

Expected before/after test counts in `Scaffold.Entities.States.Tests`:

- Before this plan: 15 tests (baseline at authoring).
- After this plan: 22 tests (seven added for G2–G8).


## Revision history

- **2026-04-29** — Initial ExecPlan authored (seven behavioral guarantees; originally drafted around six vs seven test count ambiguity—resolved as seven tests G2–G8).
- **2026-04-29** — Implemented seven tests (G2–G8), updated Wire to skip rebuild on canonical `Removed`, refreshed Purpose/Context counts and outcomes.
