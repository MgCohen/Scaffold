# State-backed entities: bridge package between `com.scaffold.entities` and `com.scaffold.states`

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

This plan supersedes Milestone 3 of `Plans/EntitiesStateBridge/EntitiesStateBridge-ExecPlan.md`. That parent plan delivered Milestones 1 (Unity decoupling) and 2 (internal restructuring of the entities package) and stopped before the bridge package. The shape of Milestone 3 changed materially after the modifier refactor (`Plans/ModifierApplyOwnership/ModifierApplyOwnership-ExecPlan.md`) deleted `EntityVariableComputer`, replaced `EntityModifierEntry.modifierValue` (a `VariableValue`) with a `VariableModifier` that owns its own `Apply`, and made order-of-application meaningful. Rather than amend the parent plan, this is a fresh, self-contained plan covering only the bridge.


## Purpose / Big Picture

Today, two packages in this repository solve adjacent problems but cannot be combined:

- `com.scaffold.entities` models gameplay objects (characters, items, cards) with named variables (`hp`, `attack`) and modifiers (`+5 hp`, `×2 attack`). Reads and writes happen synchronously in process memory: `entity.AddModifier(...)` mutates an internal handler and notifies subscribers immediately.
- `com.scaffold.states` is an immutable, transactional store. State lives in `record` slices keyed by an `IReference`; every change goes through a `Mutator<TState, TPayload>` that produces a new record. The store can `SaveSnapshot()` and `LoadSnapshot()`, which is the foundation for undo/redo, deterministic replay, and save/load.

The two cannot be combined today because every write to an `EntityInstance` bypasses the store: from the store's perspective, modifiers and variable changes are invisible. A snapshot taken before applying a modifier and restored after the modifier was applied does **not** undo the modifier — the modifier was never recorded in any slice.

After this plan, a developer can choose a "state-backed" entity by constructing a `StateEntity<TDefinition>` through a factory that wires its variable storage to the store. Reads (`entity.GetVariable<float>(hp)`) look identical to a standalone `EntityInstance`. Writes happen via `store.Execute(new AddModifierPayload(...))`. Snapshots round-trip the entity's complete variable state, including the ordered modifier stack.

You can see this working when this test passes (full code in `Validation and Acceptance` below):

    var snapshot = store.SaveSnapshot();
    store.Execute(new AddModifierPayload(instanceId, hp, new FloatAddModifier(5f), ModifierId.New()));
    Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(15f));   // 10 + 5
    store.LoadSnapshot(snapshot);
    Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(10f));   // restored

Neither core package gains a runtime dependency on the other. The bridge lives in a new package, `com.scaffold.entities.states`, that depends on both.


## Progress

- [x] Step A — Promote three internals to public in `com.scaffold.entities` (prerequisite for the bridge to fold modifiers in a mutator).
- [x] Step B — Make `Scaffold.Entities.InstanceId` implement `Scaffold.States.IReference` so the store can key slices by entity id.
- [x] Step C — Scaffold the new package `com.scaffold.entities.states` (`package.json`, runtime asmdef, tests asmdef).
- [x] Step D — Define the store slice `EntityVariableState` (immutable record holding base values, ordered modifier stacks, and cached effective values).
- [x] Step E — Define the four payload records (`AddModifierPayload`, `RemoveModifierPayload`, `SetBaseValuePayload`, `AddEntityVariablePayload`) and the four mutators that consume them.
- [x] Step F — Implement `StoreVariableStorage : IEntityVariableStorage` (reads from the store, fans out subscriptions locally).
- [x] Step G — Implement `StateEntity<TDefinition> : BaseEntityInstance<TDefinition>` and the `EntityStateFactory.Create(...)` entry point.
- [x] Step H — Write integration tests, including the snapshot round-trip and modifier-ordering tests.
- [ ] Step I — Run `.agents/scripts/validate-changes.cmd` and Unity EditMode tests; fix any diagnostics; commit.


## Surprises & Discoveries

- **Per-payload mutators must register once per `Store`.** Registering a new `AddModifierMutator` per `EntityStateFactory.Create` would stack multiple bindings for `AddModifierPayload`; each `Execute` would run every binding sequentially on the same scratchpad slice, applying the same payload more than once. Implemented **`EntityBridgeContext`** (`ConditionalWeakTable<Store, …>` + `Bind(InstanceId, IEntityDefinition)`) so the four mutators are registered on first use of a store and resolve the definition by `payload.EntityId`.
- **`Scaffold.Entities.csproj` analyzer build:** the Unity-generated VS/CLI project did not pick up the new `Scaffold.States` asmdef reference until **`ProjectReference` to `Scaffold.States.csproj`** was added to `Scaffold.Entities.csproj` (same pattern as `Scaffold.Records`). Re-sync projects from Unity if the file is regenerated without that reference.
- **`validate-changes.cmd`** EditMode/PlayMode and batch compilation exit early if another Unity instance has the project open; **`check-analyzers.ps1`** still reported `BUILD_EXIT:0`, `TOTAL:0` after the csproj fix.


## Decision Log

- Decision: Deliver Milestone 3 of `Plans/EntitiesStateBridge/EntitiesStateBridge-ExecPlan.md` as a separate ExecPlan rather than amending the parent. Rationale: the modifier refactor reshaped the slice contents, payload signatures, and mutator bodies. A clean plan is easier to follow than tracking deltas through revision history. The parent remains valid as an archive of Milestones 1 and 2 and the original design rationale. Author: Design session, 2026-04-28.
- Decision: The store slice holds an *ordered* per-variable modifier stack (`ImmutableList<ActiveModifier>`), not a `(ModifierId → modifier)` dictionary. Rationale: the modifier system now uses a fold sorted by `Order` ascending with ties broken by insertion order. A dictionary loses ordering; a list preserves both `Order` and tie-break behavior, and `ImmutableList` snapshots cleanly. Author: Design session, 2026-04-28.
- Decision: Effective values are cached inline in the slice (`EffectiveValues` field) rather than recomputed on every read. Rationale: reads are hot (UI, AI, bindings); the fold runs once per write inside the mutator, which is naturally rate-limited. Snapshot size grows by one wrapper per modified variable — acceptable. Author: Design session, 2026-04-28 (carried over from the parent plan).
- Decision: Definition defaults are *not* stored in `EntityVariableState`. The slice holds only instance-level overrides (`BaseValues`) and modifier-derived state. Reads fall through to `IEntityDefinition.TryGetDefaultValue` when no override exists. Rationale: avoids duplicating definition data in every snapshot, keeps the slice minimal, and matches the chained-bag model used by `LocalVariableStorage`. Author: Design session, 2026-04-28 (carried over from the parent plan).
- Decision: The caller pre-generates the `ModifierId` and includes it in `AddModifierPayload`. Rationale: mutators in `Scaffold.States` return only the next state; they cannot return a secondary value. Pre-generating the id keeps the mutator pure and gives the caller a stable handle for later removal. Author: Design session, 2026-04-28 (carried over from the parent plan).
- Decision: `IEntityVariableStorage` remains read-only. `StateEntity` does not implement `IMutableEntity<TDefinition>`. Writes go through the store via `Execute`. Rationale: the type system enforces the right pattern — code with a `StateEntity` cannot accidentally bypass the store; code that needs to mutate must hold a `Store` reference. Author: Design session, 2026-04-28 (carried over from the parent plan).
- Decision: `StoreVariableStorage` maintains its own per-variable callback registry rather than calling `Store.Subscribe` once per `IEntityVariableStorage.Subscribe`. Rationale: `Scaffold.States.Store.Subscribe(...)` returns `void` and exposes no `Unsubscribe` — a naive 1:1 mapping would leak callbacks. Instead, the storage subscribes to the store *once* per entity and fans out to local handlers it owns and can remove. Author: Design session, 2026-04-28.
- Decision: Register bridge mutators **once per `Store`** via `EntityBridgeContext` and resolve `IEntityDefinition` by `InstanceId`, instead of registering new mutator instances per `Create`. Rationale: the registry appends bindings per payload type; duplicate mutators would run multiple times per `Execute`. Author: implementation, 2026-04-28.


## Outcomes & Retrospective

To be written at completion.


## Context and Orientation

This section explains everything a reader needs to know about both packages to implement the bridge. Read it fully before touching any code. Repository root for all paths is `C:\Unity\Scaffold` (Windows) — paths below are repository-relative.

### What lives in `Assets/Packages/com.scaffold.entities/`

A **definition** describes a kind of entity (an HP value, an attack value). It is either an `EntityDefinition` (plain C#, constructed in code) or an `EntityDefinitionAsset` (Unity ScriptableObject, authored in the inspector). Both implement the public interface `IEntityDefinition`, defined at `Assets/Packages/com.scaffold.entities/Runtime/Core/Definitions/IEntityDefinition.cs`:

    public interface IEntityDefinition
    {
        bool TryGetDefaultValue(Variable key, out VariableValue value);
        IEnumerable<Variable> DefinedVariables { get; }
    }

A **variable** is a named, typed slot — a `Variable` record (plain serializable class) at `Runtime/Core/Variables/Variable.cs`. It carries a string `Key` (e.g. `"hp"`) and a string `PayloadTypeId` (e.g. `"float"`). Values are wrapped in a `VariableValue` subclass — `FloatVariableValue`, `IntVariableValue`, `BoolVariableValue`, `StringVariableValue` — defined under `Runtime/Core/Variables/Values/`. Each concrete value extends `VariableValue<T>` and implements `IVariableValue<T> { T Get(); }` plus `protected VariableValue<T> WithValue(T next)`.

A **modifier** is a per-variable transform expressed as a `VariableModifier<T>` subclass at `Runtime/Core/Modifiers/`. Each subclass implements `T Apply(T current)` and inherits an integer `Order` field from the non-generic base `VariableModifier`. Concrete modifiers shipping today are `FloatAddModifier`, `FloatMultiplyModifier`, `IntAddModifier`, `IntMultiplyModifier`, `BoolOverrideModifier`, `StringAppendModifier`. Adding `+5 hp` looks like `new FloatAddModifier(5f)`.

A **modifier id** is a `ModifierId` (Guid-backed `readonly struct`) at `Runtime/Core/Identity/ModifierId.cs`. `ModifierId.New()` produces a fresh value. Modifiers are removed by `(Variable, ModifierId)`, never by reference equality.

An **active modifier slot** is `ActiveModifier`, an internal `readonly struct` at `Runtime/Core/Instance/ActiveModifier.cs` — just a tuple of `(ModifierId Id, VariableModifier Modifier)`. **This struct is currently `internal` and must be promoted to `public` as part of this plan** (Step A) so the bridge package can hold it inside its store slice.

The fold itself lives on `VariableValue<T>` in `Runtime/Core/Variables/VariableValueT.cs`. The method:

    internal sealed override VariableValue ApplyModifiers(IReadOnlyList<ActiveModifier> modifiers)
    {
        T value = Get();
        for (int i = 0; i < modifiers.Count; i++)
            value = ((VariableModifier<T>)modifiers[i].Modifier).Apply(value);
        return WithValue(value);
    }

This is currently `internal`. **It must be promoted to `public` as part of this plan** (Step A) so a mutator outside the entities assembly can compute effective values without reproducing the dispatch logic.

The runtime read surface is `IReadOnlyEntity<TDefinition>` at `Runtime/Core/Contracts/IReadOnlyEntity.cs`. `BaseEntityInstance<TDefinition>` at `Runtime/Core/Instance/BaseEntityInstance.cs` implements it on top of an `IEntityVariableStorage`, exposing scalar reads:

    public T GetVariable<T>(Variable key);
    public bool TryGetVariable<T>(Variable key, out T value);
    public IDisposable Subscribe(Variable key, Action<VariableValue> onChange);
    public IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler);

The "scalar API" means callers write `entity.GetVariable<float>(hp)` and get a `float` directly; the wrapper type (`FloatVariableValue`) does not appear in caller code. Internally, the call resolves a `VariableValue` from storage and pattern-matches `is IVariableValue<T>`.

`IEntityVariableStorage` at `Runtime/Core/Instance/IEntityVariableStorage.cs` is the read-only contract `BaseEntityInstance` depends on:

    public interface IEntityVariableStorage
    {
        bool TryGetEffective(Variable key, out VariableValue value);
        bool TryGetBase(Variable key, out VariableValue value);
        IEnumerable<Variable> Variables { get; }
        IDisposable Subscribe(Variable key, Action<VariableValue> callback);
        void Unsubscribe(Variable key, Action<VariableValue> callback);
        IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler);
    }

The standalone implementation, `LocalVariableStorage` at `Runtime/Core/Instance/LocalVariableStorage.cs`, holds two bags (`instanceBaseBag`, `instanceEffectiveBag`), a `VariableModifierHandler`, and a notifier. `EntityInstance<TDefinition>` extends `BaseEntityInstance<TDefinition>` with mutating methods (`AddVariable`, `AddModifier`, `RemoveModifier`, `ClearModifiers`, ...) that delegate to the local storage.

`InstanceId` at `Runtime/Core/Identity/InstanceId.cs` is a record `InstanceId(int Id)`. **It must implement `Scaffold.States.IReference` as part of this plan** (Step B) so the store can use it as a slice key.

### What lives in `Assets/Packages/com.scaffold.states/`

A **state** is an immutable C# `record` extending `Scaffold.States.State`. State values are never mutated in place; each change produces a new record via `with`.

A **slice** is one canonical state record at one address in the store. A slice is identified by `(IReference reference, Type stateType)`. `IReference` at `Runtime/Abstractions/IReference.cs` is an empty marker interface — anything (a record, a struct) can implement it.

A **store** at `Runtime/Store.cs` holds all canonical slices. Reads use `store.Get<TState>(reference)`. Writes use `store.Execute<TPayload>(reference, payload)` which routes the payload to all registered mutators producing matching state types. Snapshots are `store.SaveSnapshot()` and `store.LoadSnapshot(snapshot)`.

A **mutator** is a `Mutator<TState, TPayload>` at `Runtime/Mutators/Mutator.cs`. Its single override is `public abstract TState Change(TState state, TPayload payload, IStateScope scope)`. Mutators are pure — they read the current state and the payload, return the next state, and never write to external objects. They are registered with `store.RegisterMutator(mutator)` or via `StoreBuilder.RegisterMutator(...)` before `Build()`.

A **store builder** at `Runtime/Builders/Store/StoreBuilder.cs` is the standard construction path: `var b = new StoreBuilder(); b.RegisterMutator(...); b.AddState(reference, initialState); var store = b.Build();`. There is also `store.RegisterSlice(reference, state)` to add a slice after construction — this is the path the bridge factory uses.

The store's subscription API at `Store.Subscribe<TState>(IReference, Action<IReference, TState, StateChangeEvent>)` returns **void**. There is no public `Unsubscribe`. This shape matters for the bridge — see Step F.

### What this plan adds

A new package `com.scaffold.entities.states` at `Assets/Packages/com.scaffold.entities.states/`. The package contains:

- A store slice `EntityVariableState` keyed by `InstanceId`.
- Four payload records and four mutators (one per write operation).
- A read-only `IEntityVariableStorage` implementation, `StoreVariableStorage`, that reads from a store.
- A subclass of `BaseEntityInstance<TDefinition>` called `StateEntity<TDefinition>`. It exposes the read API and an `InstanceId`. It does *not* implement `IMutableEntity<TDefinition>` — writes must go through the store.
- A factory `EntityStateFactory.Create(definition, store, instanceId)` that wires everything together.

Neither core package gains a dependency on the other. The bridge package depends on both via assembly references in its asmdef.


## Plan of Work

The work is one milestone with nine ordered steps. Each step ends in a state where the package compiles. Step I is the validation gate that wraps the milestone.

### Step A — Promote three entities-package internals to public

Three symbols are currently `internal` and need to be `public` for the bridge mutators to compute effective values without re-implementing the fold:

- `Assets/Packages/com.scaffold.entities/Runtime/Core/Instance/ActiveModifier.cs` — change `internal readonly struct ActiveModifier` to `public readonly struct ActiveModifier`. Both fields (`Id`, `Modifier`) are already public; only the struct's own access modifier changes.
- `Assets/Packages/com.scaffold.entities/Runtime/Core/Variables/VariableValue.cs` — change the abstract method `internal abstract VariableValue ApplyModifiers(IReadOnlyList<ActiveModifier> modifiers);` to `public abstract VariableValue ApplyModifiers(IReadOnlyList<ActiveModifier> modifiers);`.
- `Assets/Packages/com.scaffold.entities/Runtime/Core/Variables/VariableValueT.cs` — update the override signature to match: `internal sealed override VariableValue ApplyModifiers(...)` becomes `public sealed override VariableValue ApplyModifiers(...)`.

Why these three together: `ApplyModifiers` takes an `IReadOnlyList<ActiveModifier>` and is overridden in `VariableValue<T>`. Promoting one without the others is either a compile error (visibility narrower in derived than base) or useless (caller cannot construct the list type).

This is the only public-surface change to `com.scaffold.entities`. Record this in the entities README breaking-change section as part of Step I.

### Step B — Make `InstanceId` an `IReference`

The bridge slice is keyed by entity. `Store.Get<TState>(reference)` requires `reference : IReference`. In `Assets/Packages/com.scaffold.entities/Runtime/Core/Identity/InstanceId.cs`, change the record declaration:

    public record InstanceId(int Id) : IReference

and add the using directive `using Scaffold.States;` at the top. Then add an assembly reference from the entities asmdef to the states asmdef.

This makes `com.scaffold.entities` depend on `com.scaffold.states`, which the parent plan tried to avoid. The rationale for accepting it now: `IReference` is an empty marker interface (no methods, no behavior, zero runtime cost). The dependency is purely nominal and does not couple the entities runtime to any store behavior. The alternative — moving `IReference` to a third package — adds a package for a one-line interface and is overkill for the value gained.

Concretely: open `Assets/Packages/com.scaffold.entities/Runtime/Scaffold.Entities.asmdef`, add `"Scaffold.States"` to the `references` array. (If the asmdef already references it for an unrelated reason, no edit is needed; check first.) Confirm the entities runtime does **not** start `using Scaffold.States;` anywhere except the one new line in `InstanceId.cs`. Run a grep after the change:

    grep -n "Scaffold.States" Assets/Packages/com.scaffold.entities/Runtime -r

The only hit must be in `InstanceId.cs`. Any other hit means an accidental coupling crept in and must be fixed before proceeding.

### Step C — Scaffold the bridge package

Create the directory `Assets/Packages/com.scaffold.entities.states/` with this layout:

    Assets/Packages/com.scaffold.entities.states/
        package.json
        Runtime/
            Scaffold.Entities.States.asmdef
            EntityVariableState.cs
            EntityStateFactory.cs
            StateEntity.cs
            StoreVariableStorage.cs
            Mutators/
                AddModifierMutator.cs
                RemoveModifierMutator.cs
                SetBaseValueMutator.cs
                AddEntityVariableMutator.cs
            Payloads/
                AddModifierPayload.cs
                RemoveModifierPayload.cs
                SetBaseValuePayload.cs
                AddEntityVariablePayload.cs
        Tests/
            Scaffold.Entities.States.Tests.asmdef
            StateEntityIntegrationTests.cs

`package.json`:

    {
        "name": "com.scaffold.entities.states",
        "version": "0.1.0",
        "displayName": "Scaffold Entities ↔ States Bridge",
        "description": "State-backed entity instances. Modifier and variable writes flow through Scaffold.States.Store; snapshots round-trip the entity's full variable state.",
        "unity": "2022.3"
    }

`Runtime/Scaffold.Entities.States.asmdef`:

    {
        "name": "Scaffold.Entities.States",
        "rootNamespace": "Scaffold.Entities.States",
        "references": [
            "Scaffold.Entities",
            "Scaffold.States"
        ],
        "includePlatforms": [],
        "excludePlatforms": [],
        "allowUnsafeCode": false,
        "overrideReferences": false,
        "autoReferencedDefinedSymbols": [],
        "noEngineReferences": false
    }

`Tests/Scaffold.Entities.States.Tests.asmdef`:

    {
        "name": "Scaffold.Entities.States.Tests",
        "rootNamespace": "Scaffold.Entities.States.Tests",
        "references": [
            "Scaffold.Entities.States",
            "Scaffold.Entities",
            "Scaffold.States",
            "UnityEngine.TestRunner",
            "UnityEditor.TestRunner"
        ],
        "optionalUnityReferences": [
            "TestAssemblies"
        ],
        "includePlatforms": [],
        "excludePlatforms": [],
        "defineConstraints": [
            "UNITY_INCLUDE_TESTS"
        ]
    }

Every new file under `Assets/` must have a matching `.meta` file. If Unity MCP is available, create files via Unity so meta files are generated automatically. Otherwise, copy the meta-file shape from a sibling and replace the `guid` with a fresh value (PowerShell: `[System.Guid]::NewGuid().ToString("N")`).

### Step D — Define the store slice `EntityVariableState`

`Runtime/EntityVariableState.cs`:

    using System.Collections.Immutable;
    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public sealed record EntityVariableState(
            ImmutableDictionary<Variable, VariableValue> BaseValues,
            ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> ModifierStacks,
            ImmutableDictionary<Variable, VariableValue> EffectiveValues
        ) : State
        {
            public static EntityVariableState Empty { get; } = new EntityVariableState(
                ImmutableDictionary<Variable, VariableValue>.Empty,
                ImmutableDictionary<Variable, ImmutableList<ActiveModifier>>.Empty,
                ImmutableDictionary<Variable, VariableValue>.Empty);
        }
    }

What each field means in plain language:

- `BaseValues` — instance-level overrides for variables. If a variable's base differs from the definition default (e.g., a card spawned with `hp = 8` instead of the default `hp = 10`), the override lives here. If a variable is absent from `BaseValues`, the base is whatever `IEntityDefinition.TryGetDefaultValue` returns. If the definition does not know the variable either, the variable does not exist on this entity.
- `ModifierStacks` — per-variable ordered list of active modifiers. The list is sorted by `Order` ascending with ties broken by insertion order. The mutators maintain that invariant on every insert; readers (the fold) consume the list as-is.
- `EffectiveValues` — cached output of folding modifiers over the base value, recomputed by the mutator after each write. A variable is absent from `EffectiveValues` exactly when its `ModifierStacks` entry is empty (or absent), in which case the effective value equals the base value.

`System.Collections.Immutable` ships in modern Unity (2022.3+) via the .NET Standard 2.1 BCL. If `validate-changes.cmd` reports it cannot be found, add the `System.Collections.Immutable` NuGet package via `NuGetForUnity` or equivalent and record the workaround in the `Surprises & Discoveries` section.

### Step E — Define payloads and mutators

Each payload is a one-line record. Each lives in its own file under `Runtime/Payloads/` to satisfy the analyzer rule `SCA3002` (one type per file).

`Payloads/AddModifierPayload.cs`:

    using Scaffold.Entities;

    namespace Scaffold.Entities.States
    {
        public sealed record AddModifierPayload(
            InstanceId EntityId,
            Variable Variable,
            VariableModifier Modifier,
            ModifierId ModifierId);
    }

`Payloads/RemoveModifierPayload.cs`:

    using Scaffold.Entities;

    namespace Scaffold.Entities.States
    {
        public sealed record RemoveModifierPayload(
            InstanceId EntityId,
            Variable Variable,
            ModifierId ModifierId);
    }

`Payloads/SetBaseValuePayload.cs`:

    using Scaffold.Entities;

    namespace Scaffold.Entities.States
    {
        public sealed record SetBaseValuePayload(
            InstanceId EntityId,
            Variable Variable,
            VariableValue Value);
    }

`Payloads/AddEntityVariablePayload.cs`:

    using Scaffold.Entities;

    namespace Scaffold.Entities.States
    {
        public sealed record AddEntityVariablePayload(
            InstanceId EntityId,
            Variable Variable,
            VariableValue InitialValue);
    }

Note the absence of an `EntityModifierEntry` in `AddModifierPayload` — after the modifier refactor, the mutator only needs `(Variable, VariableModifier, ModifierId)`. The entry was a serialization-time pairing for asset-authored modifiers and has no role at runtime in the bridge.

Each mutator takes the `IEntityDefinition` as a constructor argument so it can read defaults during the recompute. The mutator instances are per-entity — registered in the factory at `Create(...)` time. This trades a small amount of per-entity state for a closure that does not need to look up the definition by id from a registry.

`Mutators/AddModifierMutator.cs`:

    using System.Collections.Immutable;
    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public sealed class AddModifierMutator : Mutator<EntityVariableState, AddModifierPayload>
        {
            private readonly IEntityDefinition definition;
            public AddModifierMutator(IEntityDefinition definition) { this.definition = definition; }

            public override EntityVariableState Change(
                EntityVariableState state, AddModifierPayload payload, IStateScope scope)
            {
                var bucket = state.ModifierStacks.TryGetValue(payload.Variable, out var existing)
                    ? existing
                    : ImmutableList<ActiveModifier>.Empty;

                int insertAt = 0;
                while (insertAt < bucket.Count && bucket[insertAt].Modifier.Order <= payload.Modifier.Order)
                {
                    insertAt++;
                }
                var nextBucket = bucket.Insert(insertAt, new ActiveModifier(payload.ModifierId, payload.Modifier));
                var nextStacks = state.ModifierStacks.SetItem(payload.Variable, nextBucket);

                var nextEffective = RecomputeEffective(state, nextStacks, payload.Variable, definition);

                return state with { ModifierStacks = nextStacks, EffectiveValues = nextEffective };
            }
        }
    }

`Mutators/RemoveModifierMutator.cs`:

    using System.Collections.Immutable;
    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public sealed class RemoveModifierMutator : Mutator<EntityVariableState, RemoveModifierPayload>
        {
            private readonly IEntityDefinition definition;
            public RemoveModifierMutator(IEntityDefinition definition) { this.definition = definition; }

            public override EntityVariableState Change(
                EntityVariableState state, RemoveModifierPayload payload, IStateScope scope)
            {
                if (!state.ModifierStacks.TryGetValue(payload.Variable, out var bucket))
                {
                    return state;
                }

                int idx = -1;
                for (int i = 0; i < bucket.Count; i++)
                {
                    if (bucket[i].Id.Equals(payload.ModifierId)) { idx = i; break; }
                }
                if (idx < 0) return state;

                var nextBucket = bucket.RemoveAt(idx);
                var nextStacks = nextBucket.Count == 0
                    ? state.ModifierStacks.Remove(payload.Variable)
                    : state.ModifierStacks.SetItem(payload.Variable, nextBucket);

                var nextEffective = RecomputeEffective(state, nextStacks, payload.Variable, definition);

                return state with { ModifierStacks = nextStacks, EffectiveValues = nextEffective };
            }
        }
    }

`Mutators/SetBaseValueMutator.cs`:

    using System.Collections.Immutable;
    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public sealed class SetBaseValueMutator : Mutator<EntityVariableState, SetBaseValuePayload>
        {
            private readonly IEntityDefinition definition;
            public SetBaseValueMutator(IEntityDefinition definition) { this.definition = definition; }

            public override EntityVariableState Change(
                EntityVariableState state, SetBaseValuePayload payload, IStateScope scope)
            {
                var nextBaseValues = state.BaseValues.SetItem(payload.Variable, payload.Value);
                var stateWithBase = state with { BaseValues = nextBaseValues };
                var nextEffective = RecomputeEffective(stateWithBase, stateWithBase.ModifierStacks, payload.Variable, definition);
                return stateWithBase with { EffectiveValues = nextEffective };
            }
        }
    }

`Mutators/AddEntityVariableMutator.cs`:

    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public sealed class AddEntityVariableMutator : Mutator<EntityVariableState, AddEntityVariablePayload>
        {
            private readonly IEntityDefinition definition;
            public AddEntityVariableMutator(IEntityDefinition definition) { this.definition = definition; }

            public override EntityVariableState Change(
                EntityVariableState state, AddEntityVariablePayload payload, IStateScope scope)
            {
                if (state.BaseValues.ContainsKey(payload.Variable)) return state;
                if (definition.TryGetDefaultValue(payload.Variable, out _)) return state;
                var nextBaseValues = state.BaseValues.Add(payload.Variable, payload.InitialValue);
                return state with { BaseValues = nextBaseValues };
            }
        }
    }

The shared helper `RecomputeEffective` lives in a static class next to the mutators. Add `Mutators/EffectiveValueRecomputer.cs`:

    using System.Collections.Immutable;
    using Scaffold.Entities;

    namespace Scaffold.Entities.States
    {
        internal static class EffectiveValueRecomputer
        {
            public static ImmutableDictionary<Variable, VariableValue> RecomputeFor(
                EntityVariableState state,
                ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> nextStacks,
                Variable variable,
                IEntityDefinition definition)
            {
                VariableValue baseValue = ResolveBase(state.BaseValues, variable, definition);
                if (baseValue == null) return state.EffectiveValues.Remove(variable);

                if (!nextStacks.TryGetValue(variable, out var bucket) || bucket.Count == 0)
                    return state.EffectiveValues.Remove(variable);

                VariableValue effective = baseValue.ApplyModifiers(bucket);
                return state.EffectiveValues.SetItem(variable, effective);
            }

            private static VariableValue ResolveBase(
                ImmutableDictionary<Variable, VariableValue> baseValues,
                Variable variable,
                IEntityDefinition definition)
            {
                if (baseValues.TryGetValue(variable, out var bv)) return bv;
                return definition.TryGetDefaultValue(variable, out var dv) ? dv : null;
            }
        }
    }

Each mutator's `RecomputeEffective(...)` call delegates to `EffectiveValueRecomputer.RecomputeFor(...)`. (Add the corresponding `using static` or fully-qualified call; the example mutator code above writes it as a method-call shorthand for readability.)

If the analyzer rejects nullable `VariableValue` returns, annotate types with `?` or wrap returns in `out` parameters; the API shape stays the same.

### Step F — Implement `StoreVariableStorage : IEntityVariableStorage`

`Runtime/StoreVariableStorage.cs`:

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

            private readonly Dictionary<Variable, List<Action<VariableValue>>> perVariable = new();
            private readonly List<Action<VariableStructuralChange, Variable, VariableValue>> structural = new();

            public StoreVariableStorage(Store store, InstanceId instanceId, IEntityDefinition definition)
            {
                this.store = store;
                this.instanceId = instanceId;
                this.definition = definition;

                store.Subscribe<EntityVariableState>(instanceId, OnSliceChanged);
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
                    foreach (var key in definition.DefinedVariables) yield return key;
                    foreach (var key in state.BaseValues.Keys)
                    {
                        if (!definition.TryGetDefaultValue(key, out _)) yield return key;
                    }
                }
            }

            public IDisposable Subscribe(Variable key, Action<VariableValue> callback)
            {
                if (key == null || callback == null) return EmptyDisposable.Instance;
                if (!perVariable.TryGetValue(key, out var list))
                {
                    list = new List<Action<VariableValue>>();
                    perVariable[key] = list;
                }
                list.Add(callback);
                if (TryGetEffective(key, out var current)) callback(current);
                return new VariableSubscription(this, key, callback);
            }

            public void Unsubscribe(Variable key, Action<VariableValue> callback)
            {
                if (key == null || callback == null) return;
                if (!perVariable.TryGetValue(key, out var list)) return;
                list.Remove(callback);
                if (list.Count == 0) perVariable.Remove(key);
            }

            public IDisposable SubscribeToVariableStructuralChanges(
                Action<VariableStructuralChange, Variable, VariableValue> handler)
            {
                if (handler == null) return EmptyDisposable.Instance;
                structural.Add(handler);
                return new StructuralSubscription(this, handler);
            }

            private void OnSliceChanged(IReference reference, EntityVariableState next, StateChangeEvent ev)
            {
                foreach (var pair in perVariable)
                {
                    if (TryGetEffective(pair.Key, out var current))
                    {
                        for (int i = 0; i < pair.Value.Count; i++) pair.Value[i](current);
                    }
                }
            }

            private sealed class VariableSubscription : IDisposable
            {
                private StoreVariableStorage owner;
                private Variable key;
                private Action<VariableValue> callback;
                public VariableSubscription(StoreVariableStorage o, Variable k, Action<VariableValue> c)
                { owner = o; key = k; callback = c; }
                public void Dispose()
                {
                    if (owner == null) return;
                    owner.Unsubscribe(key, callback);
                    owner = null; key = null; callback = null;
                }
            }

            private sealed class StructuralSubscription : IDisposable
            {
                private StoreVariableStorage owner;
                private Action<VariableStructuralChange, Variable, VariableValue> handler;
                public StructuralSubscription(StoreVariableStorage o,
                    Action<VariableStructuralChange, Variable, VariableValue> h)
                { owner = o; handler = h; }
                public void Dispose()
                {
                    if (owner == null) return;
                    owner.structural.Remove(handler);
                    owner = null; handler = null;
                }
            }

            private sealed class EmptyDisposable : IDisposable
            {
                public static readonly EmptyDisposable Instance = new EmptyDisposable();
                public void Dispose() { }
            }
        }
    }

The key trick: there is **one** `store.Subscribe<EntityVariableState>(instanceId, OnSliceChanged)` per storage instance, established in the constructor. All `IEntityVariableStorage.Subscribe(...)` calls return a disposable that removes the callback from a local dictionary. Disposing a per-variable subscription does not touch the store. This keeps the bridge correct in the absence of a public `store.Unsubscribe`. Document this in `Surprises & Discoveries` as soon as it is implemented.

The structural-change list is initialized but not yet driven from the slice — the existing entities `LocalVariableStorage` fires structural events when variables are added or removed, and an exact bridge implementation should compare adjacent slice states inside `OnSliceChanged` to detect those transitions. For Milestone-3 acceptance, leaving the list inert is acceptable; integration tests do not exercise it. Mark this as a known follow-up in `Outcomes & Retrospective` at completion.

The nullable-annotation parameter on the structural subscription differs from `IEntityVariableStorage.SubscribeToVariableStructuralChanges` (which uses `VariableValue?`). Either match the nullable annotation on the storage interface or use `#nullable disable` on the file; pick whichever produces zero analyzer warnings during Step I.

### Step G — Implement `StateEntity` and `EntityStateFactory`

`Runtime/StateEntity.cs`:

    using Scaffold.Entities;

    namespace Scaffold.Entities.States
    {
        public sealed class StateEntity<TDefinition> : BaseEntityInstance<TDefinition>
            where TDefinition : IEntityDefinition
        {
            internal void Setup(InstanceId id, TDefinition definition, StoreVariableStorage storage)
            {
                Initialize(id, definition, storage);
            }
        }
    }

This subclass deliberately exposes nothing beyond what `BaseEntityInstance` already provides plus its `Setup` initializer. Writes go through the store, not through the entity. The reason `Setup` exists rather than calling the protected `Initialize` directly from the factory: `Initialize` is `protected` and only reachable from within a subclass.

`Runtime/EntityStateFactory.cs`:

    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States
    {
        public static class EntityStateFactory
        {
            public static StateEntity<TDefinition> Create<TDefinition>(
                TDefinition definition,
                Store store,
                InstanceId instanceId)
                where TDefinition : IEntityDefinition
            {
                store.RegisterSlice(instanceId, EntityVariableState.Empty);
                store.RegisterMutator(new AddModifierMutator(definition));
                store.RegisterMutator(new RemoveModifierMutator(definition));
                store.RegisterMutator(new SetBaseValueMutator(definition));
                store.RegisterMutator(new AddEntityVariableMutator(definition));

                var storage = new StoreVariableStorage(store, instanceId, definition);
                var entity = new StateEntity<TDefinition>();
                entity.Setup(instanceId, definition, storage);
                return entity;
            }
        }
    }

Mutator registration is per-entity by design — each instance carries its own `IEntityDefinition` reference. If a project creates many entities sharing the same definition, this duplicates mutators in the registry. That's acceptable for the first version and correct (each mutator is keyed by `(TState, TPayload)` and merge-on-execute in the Store). Optimizing into a single registered mutator that looks up the right definition by `payload.EntityId` is a follow-up.

### Step H — Integration tests

`Tests/StateEntityIntegrationTests.cs`:

    using NUnit.Framework;
    using Scaffold.Entities;
    using Scaffold.States;

    namespace Scaffold.Entities.States.Tests
    {
        public class StateEntityIntegrationTests
        {
            private static readonly Variable Hp = new("hp", "float");

            private static (Store store, EntityDefinition def, StateEntity<EntityDefinition> entity, InstanceId id)
                MakeEntity()
            {
                var def = new EntityDefinition();
                def.AddVariable(Hp, new FloatVariableValue(10f));

                var builder = new StoreBuilder();
                var store = builder.Build();

                var id = new InstanceId(1);
                var entity = EntityStateFactory.Create(def, store, id);
                return (store, def, entity, id);
            }

            [Test]
            public void GetVariable_ReturnsDefinitionDefault_WhenNoOverridesOrModifiers()
            {
                var (_, _, entity, _) = MakeEntity();
                Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(10f));
            }

            [Test]
            public void AddModifier_ChangesEffectiveValue()
            {
                var (store, _, entity, id) = MakeEntity();
                store.Execute(id, new AddModifierPayload(id, Hp, new FloatAddModifier(5f), ModifierId.New()));
                Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(15f));
            }

            [Test]
            public void RemoveModifier_ByModifierId_RestoresPriorValue()
            {
                var (store, _, entity, id) = MakeEntity();
                var modId = ModifierId.New();
                store.Execute(id, new AddModifierPayload(id, Hp, new FloatAddModifier(5f), modId));
                store.Execute(id, new RemoveModifierPayload(id, Hp, modId));
                Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(10f));
            }

            [Test]
            public void Modifier_OrderIsRespected_AddBeforeMultiply()
            {
                var (store, _, entity, id) = MakeEntity();
                var add = new FloatAddModifier(3f);   // Order 0 by default
                var mul = new FloatMultiplyModifier(4f); // Order higher per repo convention; verify in-test
                store.Execute(id, new AddModifierPayload(id, Hp, add, ModifierId.New()));
                store.Execute(id, new AddModifierPayload(id, Hp, mul, ModifierId.New()));
                // Base 10 → +3 = 13 → ×4 = 52 if mul.Order > add.Order, otherwise (10*4)+3 = 43.
                // Assert the spec: mul has the higher Order so add runs first.
                Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(52f));
            }

            [Test]
            public void Snapshot_RoundTripsModifierStack()
            {
                var (store, _, entity, id) = MakeEntity();
                var snapshot = store.SaveSnapshot();

                store.Execute(id, new AddModifierPayload(id, Hp, new FloatAddModifier(5f), ModifierId.New()));
                Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(15f));

                store.LoadSnapshot(snapshot);
                Assert.That(entity.GetVariable<float>(Hp), Is.EqualTo(10f));
            }

            [Test]
            public void StateEntity_DoesNotImplement_IMutableEntity()
            {
                var (_, _, entity, _) = MakeEntity();
                Assert.IsFalse(entity is IMutableEntity<EntityDefinition>,
                    "StateEntity must not be assignable to IMutableEntity — writes go through the store.");
            }
        }
    }

The `Modifier_OrderIsRespected_AddBeforeMultiply` test asserts a specific arithmetic outcome. If `FloatAddModifier` and `FloatMultiplyModifier` ship with different default `Order` values than the test assumes, adjust the test to set `Order` explicitly via reflection on the serialized field, or update the asserted value to match the actual order. Document the chosen approach in the test's comment so the next reader does not have to reverse-engineer which convention is in force. The point of the test is not the specific numbers — it is that *changing the order changes the result*.

### Step I — Validate, fix, commit

From repository root `C:\Unity\Scaffold`, run the gate:

    .agents\scripts\validate-changes.cmd

Expected clean output:

    Change Validation Summary
    ----------------------------
    Scripts asmdef audit: PASS (TOTAL:0)
    Compilation: PASS (exit code 0)
    Analyzers: PASS (TOTAL:0, BLOCKERS:0)

Then run Unity EditMode tests (entities-states tests plus the existing entities tests, to confirm Step A and B did not regress anything):

    pwsh -NoProfile -File ".agents\scripts\run-editmode-tests.ps1" -TestPlatform EditMode

Expected: all tests in `Scaffold.Entities.Tests` and `Scaffold.Entities.States.Tests` pass.

If validation fails:

- Analyzer `SCA3002` (one type per file): split any file that defines multiple types. The plan already places each payload and mutator in its own file, but `EntityVariableState` plus `Empty` is fine because `Empty` is a property, not a separate type.
- Compile errors referencing `ApplyModifiers` or `ActiveModifier` from outside the entities assembly: confirm Step A's visibility promotions are saved and not stale.
- Compile errors on `store.Get<EntityVariableState>(instanceId)` complaining about the reference type: confirm Step B's `: IReference` was added and the entities asmdef references `Scaffold.States`.
- `System.Collections.Immutable` not found: see Step D; add the BCL package or fall back to `System.Collections.Generic.Dictionary` with defensive copying. Defensive copying means every mutator constructs a `new Dictionary<>(existing)` before any `.Add`/`.Remove`. Capture this fallback in `Decision Log` if used.

Commit one logical change set:

    git add Assets/Packages/com.scaffold.entities Assets/Packages/com.scaffold.entities.states Plans/StateBackedEntities
    git commit -m "Add com.scaffold.entities.states bridge package"

Do not amend or skip pre-commit hooks.


## Concrete Steps

All commands run from `C:\Unity\Scaffold` unless stated otherwise.

Bootstrapping the package directory (PowerShell on Windows):

    New-Item -ItemType Directory -Path Assets\Packages\com.scaffold.entities.states\Runtime\Mutators -Force
    New-Item -ItemType Directory -Path Assets\Packages\com.scaffold.entities.states\Runtime\Payloads -Force
    New-Item -ItemType Directory -Path Assets\Packages\com.scaffold.entities.states\Tests -Force

Validation gate (run after Step A, after Step C, and after Step I):

    .agents\scripts\validate-changes.cmd

EditMode tests (run after Step H and again after Step I):

    pwsh -NoProfile -File ".agents\scripts\run-editmode-tests.ps1" -TestPlatform EditMode

Independence grep (run after Step B and again after Step I):

    grep -rn "Scaffold.States" Assets/Packages/com.scaffold.entities/Runtime

Expected: exactly one hit, in `InstanceId.cs`. Anything else is a regression.

Update this section's checklist as work proceeds. Replace each entry with the actual command output snippet (first failure or "PASS") so a future reader can tell at a glance which steps have been validated.


## Validation and Acceptance

The change is complete when **all** of the following hold:

1. `.agents\scripts\validate-changes.cmd` reports `TOTAL:0`, `BLOCKERS:0`, exit code 0.
2. All tests in `Scaffold.Entities.Tests` pass (no regressions from Step A/B).
3. All tests in `Scaffold.Entities.States.Tests` pass — six tests, including `Snapshot_RoundTripsModifierStack` and `StateEntity_DoesNotImplement_IMutableEntity`.
4. The independence grep returns exactly one hit (`InstanceId.cs` only).
5. Running the snapshot round-trip test by hand reproduces the expected behaviour: base value `10`, after `AddModifier(+5)` value reads `15`, after `LoadSnapshot` value reads `10`.
6. A consumer can call `entity.GetVariable<float>(hp)` on a `StateEntity` and an `EntityInstance` initialized with the same definition and the same modifier and see the same result. (Spot-checked manually against the existing `EntityInstanceTests` patterns.)

The change is **not** complete if any of these fail. Failed tests are debugged, not deleted.


## Idempotence and Recovery

Each step ends in a compilable, committable state. If work is interrupted:

- After Step A: roll back `git checkout -- Assets/Packages/com.scaffold.entities/Runtime/Core/Variables/VariableValue.cs Runtime/Core/Variables/VariableValueT.cs Runtime/Core/Instance/ActiveModifier.cs` to revert the visibility promotions. Nothing else has changed.
- After Step B: revert `InstanceId.cs` and remove `"Scaffold.States"` from the entities asmdef references array.
- After Step C: delete the `com.scaffold.entities.states` directory.
- After Step D–H: rerun validation. If a partial mutator was committed and another fails, fix only the failing one — additive code is the easiest to extend.

Re-running every step in this plan is safe: file creation is overwriting, asmdef edits are idempotent (adding a reference twice is a no-op since JSON arrays are deduped on parse, but you should still avoid duplicates), and tests are pure.

If the entities asmdef ends up with a circular reference (it should not — the bridge depends on entities and states, not the other way around), confirm only `InstanceId.cs` uses `Scaffold.States` and only for the `IReference` marker. No mutator, slice, or store type should appear in `com.scaffold.entities/Runtime`.


## Artifacts and Notes

Expected `validate-changes.cmd` output after completion:

    Change Validation Summary
    ----------------------------
    Scripts asmdef audit: PASS (TOTAL:0)
    Compilation: PASS (exit code 0)
    Analyzers: PASS (TOTAL:0, BLOCKERS:0)

Expected EditMode test output snippet:

    ✓ StateEntityIntegrationTests.GetVariable_ReturnsDefinitionDefault_WhenNoOverridesOrModifiers
    ✓ StateEntityIntegrationTests.AddModifier_ChangesEffectiveValue
    ✓ StateEntityIntegrationTests.RemoveModifier_ByModifierId_RestoresPriorValue
    ✓ StateEntityIntegrationTests.Modifier_OrderIsRespected_AddBeforeMultiply
    ✓ StateEntityIntegrationTests.Snapshot_RoundTripsModifierStack
    ✓ StateEntityIntegrationTests.StateEntity_DoesNotImplement_IMutableEntity

Expected independence grep:

    Assets/Packages/com.scaffold.entities/Runtime/Core/Identity/InstanceId.cs:3:using Scaffold.States;


## Interfaces and Dependencies

Final public signatures that must exist at the end of this plan.

In `Assets/Packages/com.scaffold.entities/Runtime/Core/Instance/ActiveModifier.cs` (visibility change):

    public readonly struct ActiveModifier
    {
        public readonly ModifierId Id;
        public readonly VariableModifier Modifier;
    }

In `Assets/Packages/com.scaffold.entities/Runtime/Core/Variables/VariableValue.cs` and `VariableValueT.cs` (visibility change on `ApplyModifiers`):

    public abstract class VariableValue
    {
        public abstract VariableValue ApplyModifiers(IReadOnlyList<ActiveModifier> modifiers);
    }

In `Assets/Packages/com.scaffold.entities/Runtime/Core/Identity/InstanceId.cs`:

    public record InstanceId(int Id) : IReference;

In `Assets/Packages/com.scaffold.entities.states/Runtime/EntityVariableState.cs`:

    public sealed record EntityVariableState(
        ImmutableDictionary<Variable, VariableValue> BaseValues,
        ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> ModifierStacks,
        ImmutableDictionary<Variable, VariableValue> EffectiveValues
    ) : State
    {
        public static EntityVariableState Empty { get; }
    }

In `Assets/Packages/com.scaffold.entities.states/Runtime/Payloads/*.cs`:

    public sealed record AddModifierPayload(InstanceId EntityId, Variable Variable, VariableModifier Modifier, ModifierId ModifierId);
    public sealed record RemoveModifierPayload(InstanceId EntityId, Variable Variable, ModifierId ModifierId);
    public sealed record SetBaseValuePayload(InstanceId EntityId, Variable Variable, VariableValue Value);
    public sealed record AddEntityVariablePayload(InstanceId EntityId, Variable Variable, VariableValue InitialValue);

In `Assets/Packages/com.scaffold.entities.states/Runtime/Mutators/*.cs`:

    public sealed class AddModifierMutator     : Mutator<EntityVariableState, AddModifierPayload>     { ... }
    public sealed class RemoveModifierMutator  : Mutator<EntityVariableState, RemoveModifierPayload>  { ... }
    public sealed class SetBaseValueMutator    : Mutator<EntityVariableState, SetBaseValuePayload>    { ... }
    public sealed class AddEntityVariableMutator : Mutator<EntityVariableState, AddEntityVariablePayload> { ... }

In `Assets/Packages/com.scaffold.entities.states/Runtime/StoreVariableStorage.cs`:

    public sealed class StoreVariableStorage : IEntityVariableStorage
    {
        public StoreVariableStorage(Store store, InstanceId instanceId, IEntityDefinition definition);
        // IEntityVariableStorage — full implementation
    }

In `Assets/Packages/com.scaffold.entities.states/Runtime/StateEntity.cs`:

    public sealed class StateEntity<TDefinition> : BaseEntityInstance<TDefinition>
        where TDefinition : IEntityDefinition
    {
        // No public write surface. Does not implement IMutableEntity.
    }

In `Assets/Packages/com.scaffold.entities.states/Runtime/EntityStateFactory.cs`:

    public static class EntityStateFactory
    {
        public static StateEntity<TDefinition> Create<TDefinition>(
            TDefinition definition,
            Store store,
            InstanceId instanceId)
            where TDefinition : IEntityDefinition;
    }


## Revision history

- **2026-04-28** — Initial ExecPlan authored as a focused replacement for Milestone 3 of `Plans/EntitiesStateBridge/EntitiesStateBridge-ExecPlan.md`. Slice shape changed from `Dictionary<ModifierId, EntityModifierEntry>` to `ImmutableList<ActiveModifier>` to preserve the `Order`-then-insertion-order required by the modifier-apply-ownership refactor. `AddModifierPayload` simplified to carry `(VariableModifier, ModifierId)` directly instead of an `EntityModifierEntry`. Mutators now compute effective values via the public `VariableValue.ApplyModifiers(IReadOnlyList<ActiveModifier>)` introduced in Step A. `StoreVariableStorage` reworked to subscribe once globally and fan out locally because `Scaffold.States.Store.Subscribe` returns `void` with no public unsubscribe path. Author: design session, 2026-04-28.
