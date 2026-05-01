# SUPERSEDED — Stateless entity-state bridge: definition-as-state plus registry safety

> **This plan was superseded by [`Plans/EntityAsAggregate/EntityAsAggregate-ExecPlan.md`](../EntityAsAggregate/EntityAsAggregate-ExecPlan.md) and was never implemented.** The follow-up plan delivers the same outcome (entity owns its definition, no bridge bookkeeping) using the framework's existing `AggregateSlice` primitive, which removes more code than the design captured below. Kept here as a reference for the design conversation that led to the chosen approach. Do not implement this plan.

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. Repository policy for ExecPlans is defined in `PLANS.md` at the repository root; this document is maintained in accordance with that file.

This plan is a follow-up to `Plans/StateBackedEntities/StateBackedEntities-ExecPlan.md`, which delivered the bridge package `com.scaffold.entities.states`. That plan introduced `EntityBridgeContext` as a per-`Store` singleton kept alive in a static `ConditionalWeakTable<Store, EntityBridgeContext>`, holding a `Dictionary<InstanceId, IEntityDefinition>` plus the four bridge mutators. The bridge worked, but during a post-implementation review we found the internals carried two responsibilities — "register the four mutators on this store, exactly once" and "look up the right `IEntityDefinition` for the entity addressed by a payload" — and that conflation was the only reason the static table, the lock, and the lifecycle gymnastics existed. Splitting those responsibilities makes the bridge dramatically simpler.


## Purpose / Big Picture

Today, every `EntityStateFactory.Create(definition, store, instanceId)` call goes through `EntityBridgeContext.CreateForStore(store)`, which atomically (under a process-global lock) checks a `ConditionalWeakTable` for an existing context, creates one if absent, registers four mutators on the `Store`, and stashes the entity's `IEntityDefinition` in a per-Store dictionary. The mutators close over that context object and call `context.TryGetDefinition(payload.EntityId, out var definition)` whenever they need a default value. None of this is observable from outside the package, but anyone reading the bridge has to reason about weak references, double-checked locking, and a static singleton that survives across tests.

After this plan, the bridge has no static instance state at all. Definitions live as a normal slice in the `Store` (a new `EntityDefinitionState` record keyed by `InstanceId`). Mutators are stateless and read the definition via the same `IStateScope.Get<TState>(reference)` API they would use for any cross-slice read. `EntityBridgeContext` shrinks to a single static method, `RegisterMutators(Store store)`, called once by whoever sets up the store. The factory registers two slices per entity (variable state + definition) and never touches the mutator registry. As a separate, independent improvement that prevents this whole class of bug from recurring, `Scaffold.States.MutatorRegistry.Register` is changed to throw `DuplicateMutatorRegistrationException` when the same `(TState, TPayload, mutator concrete type)` is registered twice on the same registry. That check makes the original "stack two bindings, run them both" footgun loud at setup rather than silent at runtime.

A novice reader can see this working through three observable changes. First, `Assets/Packages/com.scaffold.entities.states/Runtime/EntityBridgeContext.cs` is reduced to roughly ten lines containing only the static `RegisterMutators` helper. Second, the existing test `TwoEntities_AddModifierAppliesOnceToTargetOnly` continues to pass, demonstrating that the new design still solves the multi-entity dispatch bug. Third, a new test in the `Scaffold.States` package shows that calling `MutatorRegistry.Register(...)` twice with two instances of the same concrete mutator type throws.


## Progress

- [ ] Step 1 — Add `EntityDefinitionState` slice in the bridge package, keyed by `InstanceId`, holding a single `IEntityDefinition` reference.
- [ ] Step 2 — Convert the four bridge mutators (`AddModifierMutator`, `RemoveModifierMutator`, `SetBaseValueMutator`, `AddEntityVariableMutator`) to stateless: drop the `EntityBridgeContext` constructor argument, replace `context.TryGetDefinition(payload.EntityId, out def)` with `scope.Get<EntityDefinitionState>(payload.EntityId)?.Definition`.
- [ ] Step 3 — Strip `EntityBridgeContext` to a static utility class with one method, `RegisterMutators(Store store)`. Delete the static `ConditionalWeakTable`, the lock, the per-Store map, the `Bind` and `TryGetDefinition` instance methods, and the `Dictionary<InstanceId, IEntityDefinition>` field.
- [ ] Step 4 — Update `EntityStateFactory.Create` to register the variable-state slice and the new definition slice on the store, and stop calling `CreateForStore`. The factory must not register mutators.
- [ ] Step 5 — Update existing setup code (tests today; future samples eventually) to call `EntityBridgeContext.RegisterMutators(store)` exactly once at store construction.
- [ ] Step 6 — Update or rewrite the dispatch-verification test `DuplicateMutatorRegistration_AppliesPayloadTwice` so it still proves the underlying behavior given the new stateless mutator shape.
- [ ] Step 7 — Add dedupe enforcement to `Assets/Packages/com.scaffold.states/Runtime/Pipeline/MutatorRegistry.cs`: throw a new public `DuplicateMutatorRegistrationException` when `Register<TState, TPayload>` is called and a binding already exists for the same `(TState, TPayload, mutator.GetType())` triple. Add a regression test in `Scaffold.States.Tests`.
- [ ] Step 8 — Validation gate: `.agents\scripts\check-analyzers.ps1` reports `BUILD_EXIT:0`, `TOTAL:0`; EditMode tests for `Scaffold.Entities.States.Tests` and `Scaffold.States.Tests` are all green; the dispatch verification test in the bridge no longer relies on the old `EntityBridgeContext` instance API.


## Surprises & Discoveries

To be filled in as work proceeds.


## Decision Log

- Decision: definitions are stored in a `Scaffold.States.State` slice (`EntityDefinitionState`) rather than in a bridge-internal map. Rationale: this is the natural home for per-entity authoring data once `InstanceId` already implements `IReference`; it removes all static state from the bridge package and makes lifecycle automatic (the slice is unregistered when the store is collected); reads from inside a mutator use the same `IStateScope.Get<TState>` path the framework already provides. Author: design conversation, 2026-04-29.
- Decision: `EntityBridgeContext` becomes a static utility with one method. Rationale: with definitions stored as state, the only remaining responsibility is "register the four mutators on this store, exactly once at setup", which is a one-shot operation — there is nothing left for an instance to hold. Author: design conversation, 2026-04-29.
- Decision: setup code (whoever builds the `Store`) explicitly calls `EntityBridgeContext.RegisterMutators(store)` once. The factory does not silently bootstrap. Rationale: the dispatch bug we are protecting against happens when registration is silent and per-entity; making the call explicit and one-shot at setup time pushes responsibility outward and turns mistakes into setup-time errors rather than gameplay-time corruption. Author: design conversation, 2026-04-29.
- Decision: `MutatorRegistry` dedupes by `(TState, TPayload, mutator concrete runtime type)`, not by instance reference and not by payload type alone. Rationale: instance-level dedupe (`==`) would not have caught the original bug where two different `AddModifierMutator` instances were registered (one per entity definition); payload-type-only dedupe would forbid legitimate fan-out where, for example, a `LogMutator<TState>` and an `ApplyMutator<TState>` both subscribe to the same payload type. Concrete-type dedupe rejects the actual accidental case while preserving the legitimate one. Author: design conversation, 2026-04-29.
- Decision: storing `IEntityDefinition` directly in `EntityDefinitionState` (a single reference) is acceptable even though the parent plan explicitly rejected "duplicating definition data in every snapshot". Rationale: that prior decision was about baking *values* (every default for every variable) into the slice, growing the snapshot proportionally to the variable count. A single object reference per entity is essentially free in memory and zero-bytes on a typical in-memory `Snapshot.SaveSnapshot()` round trip — definitions are long-lived authoring objects (typically `ScriptableObject` instances) that outlive snapshots. Author: design conversation, 2026-04-29.


## Outcomes & Retrospective

To be written at completion.


## Context and Orientation

This section explains everything a reader needs to know about both packages to implement the refactor. Read it fully before touching any code. Repository root for all paths is `C:\Unity\Scaffold` on Windows; paths below are repository-relative.

### What `com.scaffold.entities.states` looks like today

The bridge package lives at `Assets/Packages/com.scaffold.entities.states/`. Its runtime asmdef declares dependencies on `Scaffold.Entities`, `Scaffold.Records` (for the `IsExternalInit` shim that lets records compile), and `Scaffold.States`. The package contains:

- `Runtime/EntityVariableState.cs` — a `Scaffold.States.State` record `(BaseValues, ModifierStacks, EffectiveValues)` keyed by `InstanceId`. Holds instance-level base overrides, ordered modifier stacks, and cached effective values. Fields are `IReadOnlyDictionary` / `IReadOnlyList`; mutators copy-on-write through the `CreateMutableValues` and `CreateMutableStacks` helpers.
- `Runtime/EntityBridgeContext.cs` — the per-`Store` singleton that this plan will dismantle. Currently holds a static `ConditionalWeakTable<Store, EntityBridgeContext>` plus a lock; instances hold a `Dictionary<InstanceId, IEntityDefinition>` and four mutator registrations.
- `Runtime/EntityStateFactory.cs` — a public static class with `Create<TDefinition>(TDefinition, Store, InstanceId)` that calls `EntityBridgeContext.CreateForStore(store)`, then `context.Bind(instanceId, definition)`, registers the variable-state slice with `EntityVariableState.Empty`, builds a `StoreVariableStorage`, and returns a wired `StateEntity<TDefinition>`.
- `Runtime/StateEntity.cs` — a tiny subclass of `BaseEntityInstance<TDefinition>` exposing only the read API (no `IMutableEntity` implementation). Writes go through `store.Execute(...)`.
- `Runtime/StoreVariableStorage.cs` — the `IEntityVariableStorage` implementation. Reads from the store, fans subscriptions out locally, and never mutates.
- `Runtime/Mutators/` — `AddModifierMutator`, `RemoveModifierMutator`, `SetBaseValueMutator`, `AddEntityVariableMutator`, plus the static `EffectiveValueRecomputer` helper. Each mutator is `internal sealed`, takes an `EntityBridgeContext` constructor argument, and uses it to resolve the definition during `Change`.
- `Runtime/Payloads/` — `AddModifierPayload`, `RemoveModifierPayload`, `SetBaseValuePayload`, `AddEntityVariablePayload`. All `public sealed record` types. Each carries an `InstanceId EntityId` field that identifies the target entity.
- `Tests/StateEntityIntegrationTests.cs` — nine tests including `Snapshot_RoundTripsModifierStack`, `TwoEntities_AddModifierAppliesOnceToTargetOnly`, and `DuplicateMutatorRegistration_AppliesPayloadTwice` (which today exercises the bridge's internal path through `EntityBridgeContext.CreateForStore`).

### How `Scaffold.States` dispatch works

A `Mutator<TState, TPayload>` has one override, `TState Change(TState state, TPayload payload, IStateScope scope)`. The scope is the `MutatorRunner` itself, which exposes `Get<TState>(IReference reference)` for cross-slice reads against the in-flight scratchpad (which falls back to canonical state for slices not yet touched in the overlay). Mutators register with `Store.RegisterMutator(...)`, which delegates to `MutatorRegistry.Register<TState, TPayload>`. The registry holds a `Dictionary<Type, List<IPayloadMutatorBinding>>` keyed by `typeof(TPayload)` and **appends** every registration to the list with no dedupe. On `Store.Execute(reference, payload)`, the runner reads the full binding list for the payload's runtime type and runs every binding sequentially. Each binding reads the slice at `executeReference` from the scratchpad, calls `mutator.Change(...)`, and writes the result back to the same cell — which means binding N sees binding N-1's output. This is the dispatch behavior verified by `DuplicateMutatorRegistrationException_AppliesPayloadTwice`: two registered mutators applied a `+5` modifier twice, producing a base-10 entity with effective value 20 and a bucket of size 2.

### Why the existing bridge needed `EntityBridgeContext`

The original plan's `EntityStateFactory.Create` registered four new mutator instances on the store **per entity created**, each closed over that entity's `IEntityDefinition`. With two entities sharing a store, each `Execute(payload)` for one entity ran every mutator in the list — two registered `AddModifierMutator` instances meant the modifier applied twice. `EntityBridgeContext` was introduced as a per-store singleton that registered the mutators exactly once and exposed a `Bind(InstanceId, IEntityDefinition)` method so a single mutator could resolve the right definition for any payload. The static `ConditionalWeakTable` and the lock existed to enforce the singleton-per-store invariant safely across hypothetical multi-threaded creations.

This plan removes the need for any of that by: (a) putting the definition in a slice so the mutator can find it via the standard state API, and (b) making mutator registration explicit and one-shot at store-setup time so the bridge no longer needs to track "did I already register on this store?".


## Plan of Work

The work is one milestone with eight ordered steps. Every step ends in a state where the package compiles and existing tests pass. Step 8 is the validation gate.

### Step 1 — Add `EntityDefinitionState` slice

Create `Assets/Packages/com.scaffold.entities.states/Runtime/EntityDefinitionState.cs`. It is a `Scaffold.States.State` record carrying a single `IEntityDefinition` reference. The state is registered once per entity (in `EntityStateFactory.Create`) and never mutated; mutators read it but never write it. Use a `public sealed record` to match the rest of the bridge slices.

The slice does not need an `Empty` static or any helper methods. `IEntityDefinition` may be null only as a defensive contract; the factory always passes a non-null definition.

### Step 2 — Stateless mutators

Edit each of the four mutators in `Assets/Packages/com.scaffold.entities.states/Runtime/Mutators/`. Remove the `EntityBridgeContext context` field and constructor parameter. Inside `Change`, replace the existing definition lookup with a slice read against the current scope:

    var defState = scope.Get<EntityDefinitionState>(payload.EntityId);
    var definition = defState?.Definition;
    if (definition == null) return state;

Pass `definition` to `EffectiveValueRecomputer.RecomputeFor` exactly as before. Mutator class declarations stay `internal sealed`.

`EffectiveValueRecomputer` does not change in this step — it already takes an `IEntityDefinition` argument.

### Step 3 — Strip `EntityBridgeContext`

Replace the entire body of `Assets/Packages/com.scaffold.entities.states/Runtime/EntityBridgeContext.cs` with a minimal static utility:

    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public static class EntityBridgeContext
        {
            public static void RegisterMutators(Store store)
            {
                store.RegisterMutator(new AddModifierMutator());
                store.RegisterMutator(new RemoveModifierMutator());
                store.RegisterMutator(new SetBaseValueMutator());
                store.RegisterMutator(new AddEntityVariableMutator());
            }
        }
    }

The class is now `public static` (was `internal sealed`). The `RegisterMutators` method is the only public surface. The static `ConditionalWeakTable`, the lock, the `Bind` and `TryGetDefinition` methods, and the per-store dictionary are all deleted. No internal state of any kind survives.

This step elevates the class from `internal` to `public` because external setup code now has to call it. Document this change in the bridge package README under a "Setup" section.

### Step 4 — Update `EntityStateFactory.Create`

Edit `Assets/Packages/com.scaffold.entities.states/Runtime/EntityStateFactory.cs`. The new body:

    public static StateEntity<TDefinition> Create<TDefinition>(TDefinition definition, Store store, InstanceId instanceId)
        where TDefinition : IEntityDefinition
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (store == null) throw new ArgumentNullException(nameof(store));

        store.RegisterSlice(instanceId, EntityVariableState.Empty);
        store.RegisterSlice(instanceId, new EntityDefinitionState(definition));

        var storage = new StoreVariableStorage(store, instanceId, definition);
        var entity = new StateEntity<TDefinition>();
        entity.Setup(instanceId, definition, storage);
        return entity;
    }

The factory no longer touches `EntityBridgeContext`. Mutator registration is the caller's responsibility, performed once per store before any entities are created.

### Step 5 — Update setup code

Edit `Assets/Packages/com.scaffold.entities.states/Tests/StateEntityIntegrationTests.cs`. Every test currently calls `CreateEntity()` which builds a fresh store. Add a single line after `var store = new StoreBuilder().Build();`:

    EntityBridgeContext.RegisterMutators(store);

Update both the per-test inline setup in `TwoEntities_AddModifierAppliesOnceToTargetOnly`, `TwoEntities_ResolveTheirOwnDefaults`, and the helper `CreateEntity()` so that no test path can construct a store without registering the mutators.

If a future sample is added under `Assets/Packages/com.scaffold.entities.states/Samples/`, it must follow the same convention. Mention this in the package README's "Setup" section.

### Step 6 — Rewrite the dispatch-verification test

The test `DuplicateMutatorRegistration_AppliesPayloadTwice` in `StateEntityIntegrationTests.cs` currently uses `EntityBridgeContext.CreateForStore(store)` and `ctx.Bind(...)` — both of which no longer exist after Step 3. Rewrite it to:

1. Build a store and call `EntityBridgeContext.RegisterMutators(store)` once.
2. Call `store.RegisterSlice(id, EntityVariableState.Empty)` and `store.RegisterSlice(id, new EntityDefinitionState(def))` to bootstrap the entity slices manually (without going through the factory, so we control how many `AddModifierMutator` instances are registered).
3. Call `store.RegisterMutator(new AddModifierMutator())` a second time, on top of what `RegisterMutators` already registered. This simulates the original buggy condition.
4. Execute one `AddModifierPayload` with `+5`.
5. Assert that the bucket contains two entries and the effective value is 20 (base 10 + 5 + 5), proving the dispatch behavior is what we expect.

This test must continue to pass until Step 7 lands. After Step 7, the second `RegisterMutator` call will throw, and this test must be either deleted (because the underlying behavior is now impossible to reproduce) or reframed as a check that the throw happens. Choose deletion: the regression test for Step 7 will move to the `Scaffold.States` package where it logically belongs.

### Step 7 — Registry dedupe in `Scaffold.States`

Edit `Assets/Packages/com.scaffold.states/Runtime/Pipeline/MutatorRegistry.cs`. Add a new public exception type `DuplicateMutatorRegistrationException : Exception` (in its own file, `Assets/Packages/com.scaffold.states/Runtime/Pipeline/DuplicateMutatorRegistrationException.cs`, to satisfy the analyzer rule against multiple types per file). Modify `Register<TState, TPayload>` to walk the existing binding list for `typeof(TPayload)` and throw if any binding's runtime mutator type matches `mutator.GetType()`:

    public void Register<TState, TPayload>(Mutator<TState, TPayload> mutator) where TState : State
    {
        var key = typeof(TPayload);
        if (!registrations.TryGetValue(key, out var list))
        {
            list = new List<IPayloadMutatorBinding>();
            registrations[key] = list;
        }
        else
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is RegisteredMutator<TState, TPayload> registered && registered.MutatorType == mutator.GetType())
                {
                    throw new DuplicateMutatorRegistrationException($"A mutator of type {mutator.GetType().FullName} is already registered for payload {typeof(TPayload).FullName}.");
                }
            }
        }
        list.Add(new RegisteredMutator<TState, TPayload>(mutator));
    }

`RegisteredMutator<TState, TPayload>` needs to expose the runtime mutator type. Either add an `internal Type MutatorType { get; }` that returns `mutator.GetType()`, or compare on `mutator` reference identity in the registry by tracking the mutator instance directly. Pick the type-based approach so the dedupe works across distinct instances of the same class.

Add a regression test in `Assets/Packages/com.scaffold.states/Tests/`. Call it `MutatorRegistryDeduplicationTests.cs`. The test registers a small `Mutator<TestState, TestPayload>` once successfully, then calls `Register` again with a fresh instance of the same class and asserts that `DuplicateMutatorRegistrationException` is thrown. The test must use a `TestState : State` and `TestPayload` defined in the test file (not the bridge's types) so it stays isolated.

If `Scaffold.States.Tests` does not already have an analogous skeleton, create the smallest possible state/payload pair the test needs. The asmdef at `Assets/Packages/com.scaffold.states/Tests/Scaffold.States.Tests.asmdef` should already reference `Scaffold.States` and `nunit.framework` for tests.

### Step 8 — Validation gate

Run from repository root `C:\Unity\Scaffold`:

    pwsh -NoProfile -File .agents\scripts\check-analyzers.ps1

Expected: `BUILD_EXIT:0`, `TOTAL:0`.

If the user can close Unity, run:

    pwsh -NoProfile -File .agents\scripts\run-editmode-tests.ps1 -AssemblyNames Scaffold.Entities.States.Tests
    pwsh -NoProfile -File .agents\scripts\run-editmode-tests.ps1 -AssemblyNames Scaffold.States.Tests

Both must report all tests passing. Specifically:

- `Scaffold.Entities.States.Tests`: 8 tests (the 6 original integration tests, plus the two multi-entity tests added during the prior review session: `TwoEntities_AddModifierAppliesOnceToTargetOnly` and `TwoEntities_ResolveTheirOwnDefaults`). The dispatch-verification test from Step 6 has been deleted, replaced by the registry-level test in `Scaffold.States.Tests`.
- `Scaffold.States.Tests`: existing test count plus one new test, `MutatorRegistryDeduplicationTests.RegisterTwice_Throws`.

If validation fails, fix the failing step before progressing. Do not skip pre-commit hooks. Commit one logical changeset per phase: one for Steps 1–6 (the bridge refactor), one for Step 7 (the registry safety improvement). The two commits are independent and could in principle land on separate branches; the bundling here is a convenience.


## Validation and Acceptance

The change is complete when **all** of the following hold:

1. `EntityBridgeContext.cs` is approximately ten lines long and contains no static fields, no locks, no `Dictionary`, no `ConditionalWeakTable`. A `git diff` against `main` shows the bulk of the file as deletions.
2. `Assets/Packages/com.scaffold.entities.states/Runtime/EntityDefinitionState.cs` exists and is a one-field record `(IEntityDefinition Definition)`.
3. The four bridge mutators have parameterless constructors and look up their definition via `scope.Get<EntityDefinitionState>(payload.EntityId)`.
4. `EntityStateFactory.Create` registers two slices and never calls `RegisterMutator`. Setup code calls `EntityBridgeContext.RegisterMutators(store)` exactly once before creating entities.
5. `MutatorRegistry.Register` throws `DuplicateMutatorRegistrationException` when called twice with two instances of the same concrete mutator type for the same `(TState, TPayload)` pair, demonstrated by `MutatorRegistryDeduplicationTests.RegisterTwice_Throws`.
6. `.agents\scripts\check-analyzers.ps1` reports `BUILD_EXIT:0`, `TOTAL:0`.
7. EditMode tests for both `Scaffold.Entities.States.Tests` and `Scaffold.States.Tests` are all green.
8. The multi-entity behavior remains correct: `TwoEntities_AddModifierAppliesOnceToTargetOnly` continues to pass with hero == 15 and goblin == 30 after a single `+5` modifier on the hero, proving the new design solves the original dispatch bug.

The change is **not** complete if any of these fail. Failed tests are debugged and root-caused, not deleted.


## Idempotence and Recovery

Each step ends in a compilable, committable state. If work is interrupted between steps, re-run the validation gate from Step 8 to confirm the current state is consistent before proceeding.

If Step 7 introduces breakage in any other consumer of `MutatorRegistry.Register` (any bridge or sample that, prior to this plan, registered the same mutator type twice on purpose), the breakage is the desired signal — investigate the consumer, do not weaken the dedupe. The dedupe key is deliberately concrete-type-based, not instance-based, because the original bug involved distinct instances of the same class.

If `IStateScope.Get<TState>` does not in fact fall back from scratchpad to canonical for slices not yet touched in the overlay (this assumption is made in Step 2), the plan needs a small addition: `EntityStateFactory.Create` must register the `EntityDefinitionState` slice **before** any entity-state mutator can run. In practice, since slices are registered at create-time and reads happen during `Execute`, the slice will already be in canonical state when any mutator runs. Verify this assumption empirically by running the EditMode tests after Step 4 and observing that mutators successfully read the definition slice.


## Artifacts and Notes

Expected `EntityBridgeContext.cs` after Step 3 (full file):

    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public static class EntityBridgeContext
        {
            public static void RegisterMutators(Store store)
            {
                store.RegisterMutator(new AddModifierMutator());
                store.RegisterMutator(new RemoveModifierMutator());
                store.RegisterMutator(new SetBaseValueMutator());
                store.RegisterMutator(new AddEntityVariableMutator());
            }
        }
    }

Expected `EntityDefinitionState.cs` (full file):

    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public sealed record EntityDefinitionState(IEntityDefinition Definition) : State;
    }

Expected `Scaffold.States.MutatorRegistry` test (full file):

    using NUnit.Framework;

    namespace Scaffold.States.Tests
    {
        public class MutatorRegistryDeduplicationTests
        {
            private sealed record TestState(int Value) : State;
            private sealed record TestPayload();
            private sealed class TestMutator : Mutator<TestState, TestPayload>
            {
                public override TestState Change(TestState state, TestPayload payload, IStateScope scope) => state;
            }

            [Test]
            public void RegisterTwice_Throws()
            {
                var registry = new MutatorRegistry();
                registry.Register(new TestMutator());
                Assert.Throws<DuplicateMutatorRegistrationException>(() => registry.Register(new TestMutator()));
            }
        }
    }


## Revision history

- **2026-04-29** — Initial ExecPlan authored after a review session of the StateBackedEntities plan's bridge package. Goal: remove all static instance state from `EntityBridgeContext` by storing entity definitions as a normal store slice (`EntityDefinitionState`), reducing `EntityBridgeContext` to a one-shot setup utility, and adding registry-level dedupe enforcement in `Scaffold.States` so the original dispatch bug becomes a loud setup error rather than silent runtime corruption. Author: design conversation, 2026-04-29.
