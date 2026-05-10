# Shared Variables Package — Design Notes

Status: **proposal / pre-decision**. Captures the design discussion
around extracting a shared variable abstraction so the
GraphFlow blackboard, the Entities package, and the States bridge can
each plug into a common bag/handle interface while keeping their own
storage and semantics.

Companion to `Plans/GraphFlow/BlackboardVariables.md` (which raises the
"how do we bind blackboard to state/entity?" question this doc answers).

---

## Context — three packages, three variable systems

Each of the following packages independently grew a "variable" concept.
They overlap in shape but diverge in storage, identity, and write
semantics:

1. **GraphFlow blackboard** (`com.scaffold.graphflow`,
   `Runtime/Variables/`) — typed mutable cells with synchronous
   setters and `Changed` events. Zero-boxing on hot paths is a stated
   constraint. Current integration seam: `runner.CreateParentBag()`
   returns an `IVariableBag?` — **insufficient for state-backed
   binding under snapshots** (see Invariant 2).
2. **Entities** (`com.scaffold.entities`,
   `Runtime/Core/Variables/`, `Runtime/Core/VariableBags/`) —
   serialized `Variable` keys + polymorphic `VariableValue` storage,
   plus modifier stacks via `IEntityVariableStorage`. No
   per-variable change events.
3. **States bridge** (`com.scaffold.entities.states`,
   `Runtime/`) — entity variables as immutable `EntityState` records
   in a `Store`; mutations go through registered
   `Mutator<TState, TPayload>`. Reads pull-through; subscribe fires
   per-slice with the whole record.

Two `IVariableBag` interfaces already exist with the same name and
incompatible shapes:

- `Scaffold.Entities.IVariableBag` — `Parent`, `TryGetBase(Variable,
  out VariableValue)`, `LocalKeys`. (`IVariableBag.cs:6-13`)
- `Scaffold.GraphFlow.IVariableBag` — `Parent`, `TryGetCell<T>(id,
  out VariableCell<T>)`, `TryGetCell(id, out VariableCell)`.
  (`BlackboardVariables.md:386-397`)

That collision is the smell that prompted this design. The States
bridge has no native variable abstraction — it routes through entity
APIs that ultimately dispatch payloads, with no per-variable
notification path.

## Verified facts (current state)

Confirmed against the code while drafting this proposal:

- **`Store.LoadSnapshot` fires per-slice events.** `ApplySnapshot`
  calls `Set(r, value)` on existing slices (notifies `Updated`) and
  `ReregisterCanonicalSliceFromSnapshot` on new ones (notifies
  `Created`); `PruneCanonicalSlicesNotInSnapshot` calls
  `UnregisterSlice` on absent ones (notifies `Removed`).
  (`Store.cs:179-223`) — A mirror-cell adapter built on `Subscribe`
  follows snapshots automatically.
- **`Set` mutates the existing slice in place.** `slice.Set(state)`
  preserves the slice instance, so handle/cell references can stay
  stable across snapshot loads as long as the bag adapter doesn't
  rebuild handles. (`Store.cs:494-503`)
- **Entity has zero per-variable change-notification surface.**
  `EntityInstance` (`EntityInstance.cs:8-65`) — no events.
  `IEntityVariableStorage` (`IEntityVariableStorage.cs:6-21`) — pure
  get/set. `LocalVariableStorage`, `EntityState` — none. The entity
  `VariableBag` only fires `OnVariableStructuralChange` for add/remove
  (`VariableBag.cs:20`), not on value writes.
- **The only path to "entity variable changed" is
  `store.Subscribe<EntityState>(entityRef, ...)`** — fires once per
  slice commit with the *whole* `EntityState` record. Any per-variable
  binding has to project + diff per cell.
- **Entity uses string-based type identity.** `Variable.PayloadTypeId`
  is `"int" | "float" | "string" | ...`, not a `System.Type`.
  (`Variable.cs:8-28`) GraphFlow uses `System.Type` directly. The
  shared abstraction has to bridge these.
- **`VariableValue` is polymorphic-boxed.** Abstract `[Serializable]`
  base; subclasses wrap typed values. (`VariableValue.cs:9-13`) Entity
  pays for boxing; GraphFlow's `VariableCell<T>` does not.

## Proposal — extract `com.scaffold.variables`

A small, deliberately minimal package that owns the *interface*, not
the storage. Each consumer keeps its own representation and adopts the
shared abstraction at the boundary.

### What's in scope

- **`Variable`** — identity. `(string id, string typeName)`. Serializable.
  Equality on `id`. `typeName` is descriptive; resolves to `System.Type`
  once on first use via a registry.
- **`IVariableHandle` / `IVariableHandle<T>`** — the typed accessor.
- **`IVariableBag`** — the `Parent`-chained lookup interface.
- **`VariableDefault` / `VariableDefault<T>`** — designer-authored
  seeds with `[SerializeReference]`. Already proven in GraphFlow.
- **`InMemoryHandle<T>` / `InMemoryVariableBag`** — default
  implementation, identical in shape to GraphFlow's existing
  `VariableCell<T>` / `InMemoryVariableBag`.

### What stays per-package

- **Modifier stacks** — entity-specific.
  `IEntityVariableStorage : IVariableBag` adds modifier methods.
- **Storage representation** — `VariableCell<T>` (graphflow's mutable
  cell), `EntityState` (states' immutable record), `VariableBag`
  entries (entities' serialized list) all stay where they are.
- **Write semantics** — graphflow writes synchronously to a cell,
  states dispatch payloads, entities go through storage. The shared
  `IVariableHandle<T>.Set` looks synchronous to callers (because
  `Store.Execute` is sync), but the underlying mechanism stays
  per-package.

## Design invariants — non-negotiable

Two constraints the proposal must satisfy. Both are non-negotiable;
any phase that compromises them stalls.

### Invariant 1 — Single canonical variable system per package

After migration, no package may have two parallel variable systems.
The shared `Scaffold.Variables` types are *the* variable abstraction;
existing per-package types are **deleted, not deprecated-and-kept**.
A package may ship its own `IVariableBag` *implementation*
(state-backed, modifier-aware, save-system-backed) but never its own
*interface*.

Concretely:

- GraphFlow's existing `Scaffold.GraphFlow.IVariableBag`,
  `VariableCell<T>`, `InMemoryVariableBag`, and `VariableDefault`
  hierarchy are **deleted in Phase A**, not aliased. Imports update
  to the shared namespace. No re-export shims.
- Entity's `Scaffold.Entities.IVariableBag` and `Variable` (the key
  class) are **deleted in Phase B / D**, not aliased. The
  `[Obsolete]` extension method for `TryGetBase` is genuinely
  transitional — Phase D removes it. After Phase D the codebase has
  exactly one `IVariableBag` interface.
- Per-package custom bag *implementations* are encouraged
  (`InMemoryVariableBag`, `EntityBag`, `StoreBackedVariableBag`).
  What's prohibited is duplicate *interface* surface.

Implication: the migration plan has no "soft-launch" exit. Every
step that adds a shared type commits to deleting the legacy
counterpart in a later step.

### Invariant 2 — Storage replacement, not parent chaining

For state ↔ graphflow binding to be coherent under the snapshot
system, graphflow's graph-layer bag must be **fully replaceable** by
the consumer — not merely chained to as a parent.

Today's plan exposes `runner.CreateParentBag()` as the integration
seam. That isn't enough: variables declared on the graph asset
(seeded into `runner.Variables` via the default `InMemoryVariableBag`)
bypass any state-backed parent bag entirely. They live in-memory,
don't participate in `Store.SaveSnapshot()`, and are silently
ephemeral. A consumer that "saves a variable to the blackboard"
breaks save/load with no diagnostic.

**Resolution.** Replace `CreateParentBag()` with
`CreateVariableBag(IEnumerable<RuntimeVariable> seed)`. Consumer
gets full control over how every graph-layer variable is
materialized:

```csharp
// Default: in-memory bag seeded with authored defaults.
protected internal virtual IVariableBag CreateVariableBag(
    IEnumerable<RuntimeVariable> seed)
    => new InMemoryVariableBag(seed);

// Consumer override: state-backed bag with explicit bindings.
protected override IVariableBag CreateVariableBag(
    IEnumerable<RuntimeVariable> seed)
{
    return new StoreVariableBagBuilder(_store)
        .ForEntity(_heroRef)
            .BindBase<float>(hpVarId, hpVar)
        .ForSlice<TurnState>(_gameRef)
            .Bind<int>(turnVarId, s => s.Turn, v => new SetTurnPayload(v))
        .WithFallback(seed, FallbackMode.InMemoryDefault)
        .Build();
}
```

The fallback mechanism handles graph-declared variables the consumer
didn't explicitly bind:

- `FallbackMode.InMemoryDefault` — unbound variables get an
  `InMemoryHandle<T>` seeded with their authored default (hybrid
  storage; transient/scratch variables stay in-memory).
- `FallbackMode.Throw` — unbound variables are a build error
  (strict; everything must be state-backed).

The flow-layer bag stays as-is — per-Run scratch is intentionally
ephemeral and not snapshot-relevant. If per-flow declarations land
later (per `BlackboardVariables.md` Q7), a sibling
`CreateFlowVariableBag(seed)` seam follows the same pattern.

**Implication.** Phase A is no longer a pure namespace swap. The
graphflow runner gets a real architectural change: `SeedVariables`
goes away, `CreateVariableBag(seed)` replaces `CreateParentBag()`,
and the seam is repositioned. ~1–2 extra days; **Phase A estimate
revised to ~5–7 person-days.** Total end-to-end estimate ticks up
to ~17–18 person-days.

## Sketch — proposed interfaces

```csharp
namespace Scaffold.Variables
{
    [Serializable]
    public sealed class Variable : IEquatable<Variable>
    {
        [SerializeField] string id;        // GUID, stable identity
        [SerializeField] string typeName;  // serialization-friendly type tag
        public string Id       => id;
        public string TypeName => typeName;
        // Equality on Id only; typeName is descriptive.
    }

    public interface IVariableHandle
    {
        string Id { get; }
        Type   Type { get; }
    }

    public interface IReadOnlyVariableHandle<T> : IVariableHandle
    {
        T Value { get; }
        void Subscribe(Action<T> handler);
        void Unsubscribe(Action<T> handler);
    }

    public interface IVariableHandle<T> : IReadOnlyVariableHandle<T>
    {
        void Set(T value);
    }

    public interface IVariableBag
    {
        IVariableBag? Parent { get; }
        bool TryGet<T>(string id, out IVariableHandle<T> handle);
        bool TryGet(string id, out IVariableHandle handle);   // introspection only
        IEnumerable<IVariableHandle> LocalHandles { get; }    // snapshot / inspector
    }

    [Serializable]
    public abstract class VariableDefault
    {
        public abstract Type ValueType { get; }
        public abstract IVariableHandle CreateHandle(string id);
    }

    [Serializable]
    public abstract class VariableDefault<T> : VariableDefault
    {
        public T value = default!;
        public sealed override Type ValueType => typeof(T);
        public override IVariableHandle CreateHandle(string id)
            => new InMemoryHandle<T>(id, value);
    }

    public sealed class InMemoryHandle<T>     : IVariableHandle<T> { /* graphflow's VariableCell<T> */ }
    public sealed class InMemoryVariableBag   : IVariableBag       { /* graphflow's bag */ }
}
```

### `Set` contract

> "`Set` returns when the value is observable on the next `Get`."

That's the only contract that survives across mutable cells, payload
dispatch, and modifier-stacked computed reads. Don't tighten further
(e.g., "Changed fires synchronously inside Set") — store-backed
implementations can't honor that without fighting their own
architecture.

## How each package adopts the shape

### 1. GraphFlow blackboard — low to medium cost

The shapes match the proposed sketch — `VariableCell<T>` *is*
`InMemoryHandle<T>`; the existing `IVariableBag` *is* the proposed
one minus the namespace. So most of Phase A is straight namespace
migration. The non-trivial part is the seam change required by
Invariant 2.

- `InMemoryVariableBag`, `VariableCell<T>`, `IVariableBag`,
  `VariableDefault<T>` are **deleted** from
  `Scaffold.GraphFlow` and replaced with imports of the shared
  package types. No re-exports or type-aliases (Invariant 1).
- `RuntimeVariable.defaultValue` references shared `VariableDefault`.
- `GraphRunner.CreateParentBag()` is **replaced** by
  `CreateVariableBag(IEnumerable<RuntimeVariable> seed)` per
  Invariant 2. Default override returns `new InMemoryVariableBag(seed)`
  so existing consumers keep working.
- Internal `SeedVariables` plumbing on `GraphRunner` collapses —
  the runner asks the consumer to construct the bag rather than
  constructing one with consumer-supplied parent.
- Zero-boxing preserved for the in-memory case — `InMemoryVariableBag`
  with field-typed storage is the default. State-backed bags
  legitimately box on `Get` (audit-confirmed cost; only paid when
  the consumer opts into state-backing).

### 2. Entities — medium cost (real refactor)

- `IEntityVariableStorage` extends `Scaffold.Variables.IVariableBag`
  instead of being parallel.
- Modifier methods (`AddModifier`, `RemoveModifiersFromSource`, ...)
  stay on `IEntityVariableStorage` — out of the shared interface.
- `VariableBag.TryGetBase(Variable, out VariableValue)` migrates to
  `TryGet<T>(string, out IVariableHandle<T>)`. Internally still backed
  by `VariableValue` polymorphism; the typed surface unwraps.
- Entity-side handle's `Get` returns `base + modifiers` (computed);
  `Set` writes the base only.
- Entity registry maps `payloadTypeId` ("int", "float", ...) →
  `System.Type`. Lives in entities, not the shared package.
- Drawers, ScriptableObject editors, and tests revalidate.

### 3. States bridge — low cost (purely additive)

Net-new `StoreBackedVariableBag : IVariableBag` plus per-slice typed
handles. The bag holds `(Reference, Type<TState>, projector,
payloadFactory)` tuples; one `Subscribe<TState>` per distinct slice
key fans out to all handles bound to that slice.

```csharp
public sealed class StoreBackedHandle<TState, T> : IVariableHandle<T>
    where TState : State
{
    readonly Store              _store;
    readonly Reference          _ref;
    readonly Func<TState, T>    _project;
    readonly Func<T, object>    _toPayload;
    Action<T>?                  _subscribers;
    T    _last;
    bool _applyingFromSubscribe;

    public T Value => _project(_store.Get<TState>(_ref));

    public void Set(T value)
    {
        if (_applyingFromSubscribe) return;     // re-entry guard
        if (EqualityComparer<T>.Default.Equals(_last, value)) return;
        _store.Execute(_ref, _toPayload(value));
    }

    public void Subscribe(Action<T> handler)   => _subscribers += handler;
    public void Unsubscribe(Action<T> handler) => _subscribers -= handler;

    void OnSliceChanged(TState s, StateChangeEvent _)
    {
        var next = _project(s);
        if (EqualityComparer<T>.Default.Equals(_last, next)) return;
        _last = next;
        _applyingFromSubscribe = true;
        try { _subscribers?.Invoke(next); }
        finally { _applyingFromSubscribe = false; }
    }
}
```

Snapshot loads ride for free — `Store.LoadSnapshot` already fires
`Updated`/`Created`/`Removed` per slice, and the subscribe handler
re-projects.

## Strain points worth naming up front

1. **Two type-identity systems.** Entity uses `string payloadTypeId`;
   GraphFlow uses `System.Type`. Shared `Variable.TypeName` reconciles
   them, but entity needs a registry to map `"int"` → `typeof(int)`.
   That registry lives in entities, not the shared package.
2. **Modifier semantics stay private to entities.** Don't try to
   express modifiers in the shared interface. An entity-backed
   `IVariableHandle<float>.Value` returns the computed result;
   consumers who need base-only access go through the entity-specific
   surface.
3. **Equality on `Set` matters per backend.**
   - GraphFlow's cell dedupes on raw value.
   - Store-backed dedupes on projected value.
   - Entity-backed should dedupe on base-only (otherwise modifier
     stack changes fire spurious `Changed`).
   Document the rule per backend; don't try to enforce it in the
   interface.
4. **`LocalHandles` enumeration is the inspector / save hook.** Don't
   skip it. Entity's `VariableBag` already serializes `entries`, so
   introspection is free; GraphFlow's `InMemoryVariableBag` yields
   from its `Dictionary`; state-backed bag enumerates over its
   registered `(slice, projector)` mappings.
5. **Polymorphic `VariableValue` vs. generic `T` storage.** Entity
   boxes; GraphFlow doesn't. The shared `IVariableHandle<T>` is the
   typed surface; entity's handle internally boxes/unboxes,
   GraphFlow's stores `T` directly. Each package pays its own cost.

## Deeper audit — concepts the original sketch silently put at risk

Done after a full read of `com.scaffold.entities`,
`com.scaffold.states`, and `com.scaffold.entities.states`. The
interface shape above still fits all three packages, but several
load-bearing internals would be silently killed or mis-documented if
the proposal were implemented as drafted.

### Entities

1. **`VariableValueRegistry` is a real, load-bearing API** — not just
   a conceptual concern. It maps `Variable.PayloadTypeId` ("int" /
   "float" / ...) → concrete `VariableValue` subclass via a
   reflection + `[VariableValueIdAttribute]` scan
   (`VariableValueRegistry.cs`, `VariablePayloadTypeHelpers.cs`).
   Drawers and edit-time type validation depend on it. Shared
   `Variable.TypeName` does **not** replace it — entities still needs
   the registry for authoring. **Decision:** registry stays in
   entities; if other packages need string→Type resolution, expose a
   small `IPayloadTypeRegistry` interface in the shared package that
   entities implements.
2. **`VariableBag` serialization round-trip is real and external.**
   `[SerializeField] List<VariableEntry> entries` + `[NonSerialized]
   Dictionary` cache, rebuilt in `RebuildCache()`
   (`VariableBag.cs:45-52`). Drawers author into `entries`; runtime
   reads the cache. The shared `IVariableBag` is silent on
   serialization, which is fine — entity keeps its own serialized
   form *and* exposes the new typed-handle surface on top of it.
3. **`OnVariableStructuralChange` is the only existing notification
   surface in entities** (`VariableBag.cs:20`). Fires on
   `Add` / `Remove`, **not** on value writes. External tools (drawers,
   undo, inspectors) subscribe. The proposal's shared `IVariableBag`
   has no structural-change surface. **Decision:** keep these events
   on entity-specific `IEntityVariableStorage` / `VariableBag` rather
   than promoting to shared — they're entity-flavored and adding
   modifier-add/remove notifications later wants the same shape.
4. **`ModifierSource` carries `(Reference, int Tag)`** — both fields
   matter. `Reference` ties cleanup to a state slice; `Tag` lets a
   single source distinguish multiple applications (one buff stack
   vs. another from the same source). Trivially preserved in
   payloads; worth naming so a future refactor doesn't drop the
   `Tag`.

### States

5. **`DeferredStateEventHandler` has merge semantics.** Two modes:
   `PreserveAll` (replay every event) and `LatestPerKey` (one event
   per `(Reference, StateType)` with the final value). Mutator
   runners and snapshot loads run inside `BeginDeferScope()`. A
   `StoreBackedHandle.Changed` subscriber will see **at most one
   merged event per slice per Execute / snapshot load**, not one per
   write inside that batch. This is the right semantics for
   blackboard binding (cells only care about the latest value), but
   the contract has to be explicit so consumers don't expect
   intermediate observation.
6. **`IMutatorDispatcher` can silently drop unregistered payloads.**
   `Store.Execute(payload)` checks `mutatorDispatcher.TryDispatch`
   first (`Store.cs:280-283`); if a buggy / no-op dispatcher claims
   the payload type, the write disappears with no exception.
   **Decision:** `StoreBackedVariableBag` registers writer payloads
   at bag-construction time and validates during
   `EntityBridgeContext.RegisterMutators` that every payload type is
   bound. Add an integration test that a misconfigured handle throws
   on first `Set` rather than silently dropping.
7. **Aggregates are a parallel slice family** (`Store.cs:24`,
   `RegisterAggregate` at `Store.cs:429-446`) with their own lifecycle
   (`OnAttachedToStore`) and a separate `aggregates` map. The
   original proposal silently assumed every state-backed handle
   targets a canonical slice. **Decision:** v1 of
   `StoreBackedVariableBag` supports canonical slices only; document
   the limitation. Aggregate binding becomes a follow-up.

### Bridge

8. **`StoreVariableStorage.Parent` is hardcoded `null`**
   (`StoreVariableStorage.cs:21`). Shared `IVariableBag.Parent` would
   lie if a store-backed bag claimed a parent inherited from
   storage. **Decision:** `StoreBackedVariableBag` returns its parent
   from the constructor parameter (consumer-supplied, like
   graphflow's `InMemoryVariableBag`), not from the underlying
   `IEntityVariableStorage`.
9. **`EntityState.WithModifier` inserts by `Modifier.Order`**
   (`EntityState.cs:65-78`). The `IVariableHandle<T>.Set` interface
   only writes a base value; modifier add/remove stays on
   `IEntityVariableStorage`. Document explicitly: **the typed handle
   never touches modifiers**. Modifier-as-variable is not a
   supported pattern; reach for the entity-specific surface.
10. **Handles must never cache slice instances.** `WithBaseValue` /
    `WithModifier` return fresh records every commit. A handle that
    cached a slice reference would see stale data after a competing
    mutation. The sketched `StoreBackedHandle` pulls fresh on every
    `Get` (`_store.Get<TState>(_ref)`), which is correct — call this
    out as part of the contract: **handles read through the store on
    every `Get`; they cache projected `_last` for dedupe but never
    the slice itself.**

### What the audit confirms about the original sketch

- The interface shape (`Variable`, `IVariableHandle<T>`,
  `IVariableBag`) still fits all three packages.
- The `Set` contract ("returns when the value is observable on the
  next `Get`") survives once we add the per-Execute merge
  clarification (#5).
- The migration order is unchanged.
- The **entities migration is larger** than first estimated —
  registry ownership, structural-change events, and drawer
  revalidation all add weight to step 3.

## Risk triage and additional gaps

Not every audit finding above carries the same weight. Triaging by
"does this change the design or just the spec":

### Tier 1 — Real risks. Change the design or add code.

- **Audit #5 — `Changed` merge semantics.** Behaviorally consistent
  with graphflow's existing cell (same-value dedupe), so no
  surprise — but the contract has to be written and tested before
  any consumer builds on it. **Action:** document "at most one event
  per slice per `Execute` / snapshot load, with the final post-merge
  value." Add a test that asserts merged delivery during a batch
  execute.
- **Audit #6 — dispatcher silent-drop.** `Store.Execute` returning
  cleanly when nothing happened is a real footgun. **Action:**
  `StoreBackedVariableBag` validates at construction that every
  registered handle's payload type resolves to a real mutator;
  throws clearly if not. ~10 lines, prevents a class of
  impossible-to-debug failures.

### Tier 2 — Contract clarifications. No design change.

Audit #1 (registry crossing the bridge), #7 (aggregates v1 limit), #8
(bag parent from constructor not storage), #9 (handle never touches
modifiers), #10 (handle never caches slice instances). All need
explicit wording in the spec; none change the interface or the
implementation sketch.

### Tier 3 — Documentation hygiene only.

Audit #2 (serialization stays in entity), #3 (structural events stay
entity-specific), #4 (`ModifierSource.Tag` carriage). Mention once,
move on.

### Risks not surfaced by the file-level audit

1. **Reentrancy across the bag chain.** The `_applyingFromSubscribe`
   guard in `StoreBackedHandle` is per-handle. Cycle:
   handle A's `Changed` → graph reacts → writes handle B → fires B's
   Subscribe → graph reacts → writes A → loop. Per-handle guards
   don't catch cross-handle cycles. Same risk already exists for
   graphflow's in-memory cells; **accept as consumer responsibility**.
   Don't add bag-level cycle detection unless a real consumer hits
   it.
2. **Subscription teardown.** Graphflow already has Observe-node
   teardown as backlog #15 (`BlackboardVariables.md:750-752`).
   `StoreBackedVariableBag` inherits the same problem worse:
   `store.Subscribe<TState>` has no "unsubscribe by owner" API; the
   bag must track every delegate and unsubscribe in `Dispose`. **v1
   ships `StoreBackedVariableBag : IDisposable`** — non-negotiable.
3. **`IEntityVariableStorage : IVariableBag` migration mechanics.**
   Plan says "existing `TryGetBase` callers keep working." Three
   options: (a) duplicate the method on both interfaces; (b) C# 8
   default interface methods (Unity 2020.2+ supports at runtime but
   tooling support is uneven); (c) extension methods that route to
   the new `TryGet<T>`. **Pick before step 3 starts**, not during.
4. **Allocation cost.** A `StoreBackedVariableBag` binding M
   variables registers M `Subscribe` callbacks + M ledger entries
   per runner instance. For 50 variables × N runners this adds up.
   Probably fine, **measure before declaring v1 done.** Pool the
   delegates if it shows.
5. **Handle binding API surface — undesigned.** Where does the
   consumer say "this `Variable` projects from `EntityState` at
   `entityRef`, with this projector and this payload factory"? The
   original sketch hand-waved this. Realistically:

   ```csharp
   var bag = new StoreBackedVariableBagBuilder(store)
       .Bind<EntityState, float>(
           varId: hpVarGuid,
           sliceRef: entityRef,
           project: s => s.GetBase<float>(hpVar),
           toPayload: v => new SetBaseValuePayload(entityRef, hpVar, VariableValue.Of(v)))
       .Build();
   ```

   This is real API surface to design. Has to be type-safe enough
   that "wrong projector for this slice type" is a compile error,
   not a runtime one. **Open question:** does the bag-builder also
   accept a `Variable` (carrying typeName) and infer the
   payload factory via the registry, or does the consumer always
   supply both?

### Implication for v1 design

Two concrete code changes versus the original sketch:

1. **`StoreBackedVariableBag : IDisposable`** with explicit
   subscription tracking and teardown.
2. **Construction-time payload validation** that fails fast on
   missing dispatcher registration.

Plus one undesigned piece of API surface: **the binding builder**.
Spec needs a sketch before step 4 lands.

Everything else is contract wording or known follow-up work.

## Final code-audit verification

Final pass against the actual codebase before any code lands.
Verified each load-bearing claim in the design against real
file:line citations. **No blockers found.** Five caveats absorbed
into the migration plan; four claims confirmed.

### Confirmed wins

- **Seam change is mechanically clean.** `GraphRunner.SeedVariables`
  is called once from `GraphBuilder.Build`; replacing the construction
  with `CreateVariableBag(seed)` is a localized change.
- **Store API matches the builder design exactly.**
  `Store.Execute<TPayload>(Reference?, TPayload)` (`Store.cs:272`) and
  `Store.Subscribe<TState>(Reference, Action<TState, StateChangeEvent>)`
  (`Store.cs:96`) accept the parameters the builder passes verbatim.
- **The bridge crossing already exists.**
  `VariableValueFactory.From<T>(T value)` at
  `VariableValueFactory.cs:30-41` wraps a typed `T` into the
  appropriate `VariableValue` subclass via a switch-on-type. Builder's
  `toPayload` factory uses it directly; no new wrapping code needed.
- **`EntityBridgeContext.RegisterMutators` is idempotent**
  (`EntityBridgeContext.cs:9-24`). Deduplicates at the
  `MutatorRegistry.Register` layer. Per-`StoreBuilder`, not global —
  if two stores want bridge mutations, both must register, but
  re-registering against the same builder is safe.

### Caveats absorbed into the plan

1. **`InMemoryHandle<T>` exposed as concrete public type.**
   GraphFlow's port-bind closures (`Port.cs`, `InputPort.cs`) cache
   concrete cell references for hot-path zero-boxing. The shared
   package exposes both the `IVariableHandle<T>` interface AND the
   `InMemoryHandle<T>` concrete class; graphflow continues to capture
   concrete refs. **Cost: 0.** Design clarification only.
2. **`payloadTypeId` moves to `VariableEntry`.**
   `VariableKeySoField.cs:193-209` (the editor field hub) reads
   `payloadTypeId` from `Variable` today. Under shared `Variable`, the
   tag moves to entity's `VariableEntry` (the serialized authoring
   form). Editor field rewrites to read from the entry. **Phase B
   cost: +1 day.**
3. **`IVariableHandle<T>` uses `Set(T)` method, not property setter.**
   Avoids any C# subtlety around `new` keyword interface property
   shadowing. Read still uses `.Value` property; only writes go
   through `.Set(x)`. GraphFlow's existing `cell.Value = x` migrates
   to `handle.Set(x)`. **Phase A cost: +0.5 days** (call-site
   updates).
4. **`MutatorRegistry.IsRegistered(Type)` added to
   `com.scaffold.states`.** Builder needs it for construction-time
   payload validation (audit #6). Trivial public method addition.
   **Phase A cost: +0.5 days.**
5. **Asset migration for namespace change.** `Variable.cs:24-28`
   serializes `key` + `payloadTypeId` as `[SerializeField]` strings.
   When `Scaffold.Entities.Variable` becomes
   `Scaffold.Variables.Variable`, Unity loses serialized references
   in existing `.asset` files and MonoBehaviour fields unless an
   `OnAfterDeserialize` migration is provided. Sample assets under
   `Samples~/` would break without it. **Phase B cost: +2–3 days**
   for migration + test coverage.

### Updated total estimate

**~20–22 person-days end-to-end.** Was ~17–18 before this audit.
Phase A: ~6–8 days (was 5–7). Phase B: ~13–16 days (was 10–12).
Phases C / D / E unchanged.

## Migration order (to keep green)

1. **Phase A.1 — Land `com.scaffold.variables`** with `Variable`,
   `IVariableHandle<T>`, `IReadOnlyVariableHandle<T>`, `IVariableBag`,
   `VariableDefault<T>`, `InMemoryHandle<T>` (concrete public class),
   `InMemoryVariableBag`. Pure additive — no consumer changes yet.
   Also adds `MutatorRegistry.IsRegistered(Type)` to
   `com.scaffold.states` (small public method, used by Phase C
   construction-time validation).
2. **Phase A.2 — Migrate GraphFlow.** Per Invariant 1: delete
   GraphFlow's private `IVariableBag`, `VariableCell<T>`,
   `InMemoryVariableBag`, and `VariableDefault` hierarchy outright;
   imports update to `Scaffold.Variables`. Per Invariant 2: replace
   `CreateParentBag()` with `CreateVariableBag(seed)` and remove the
   internal `SeedVariables` plumbing. Default override preserves
   current behavior (`new InMemoryVariableBag(seed)`). Existing
   `cell.Value = x` call sites migrate to `handle.Set(x)`. All
   existing GraphFlow tests must stay green.
3. **Phase B — Migrate entities.** `Variable` extracted to shared.
   `payloadTypeId` moves from `Variable` to `VariableEntry` (the
   serialized authoring form). `VariableKeySoField` rewrites to
   read the tag from the entry. `IEntityVariableStorage :
   Scaffold.Variables.IVariableBag`. Add `TryGet<T>` to
   `VariableBag`. `[Obsolete]` extension method routes `TryGetBase`
   → `TryGet<T>`. **Includes asset-migration script
   (`OnAfterDeserialize`)** to fix up old `.asset` files and
   MonoBehaviour fields when `Scaffold.Entities.Variable` becomes
   `Scaffold.Variables.Variable`.
4. **Phase C — `StoreVariableBagBuilder` + state-backed bag in
   `com.scaffold.entities.states`.** Net-new code; no migration.
   `StoreBackedHandle.Set` calls
   `VariableValueFactory.From<T>(value)` to wrap into the
   appropriate `VariableValue` subclass. Includes the three-test
   deferral fixture (Q8) and construction-time payload-registration
   validation via `MutatorRegistry.IsRegistered`.
5. **Phase D — Erase the `[Obsolete]` extension methods.** All
   internal `TryGetBase` callers migrated to `TryGet<T>`. Per
   Invariant 1: codebase ends with exactly one `IVariableBag`
   interface.
6. **Phase E — Wire consumer-side blackboard ↔ entity/state.**
   First real consumer overrides `GraphRunner.CreateVariableBag`
   to return a `StoreVariableBagBuilder`-built bag. Validates the
   end-to-end story.

Each phase ends green; no consumer must adopt the new shape until
its own phase lands. Phases A → B can run in parallel if developer
capacity allows; C blocks on B; D blocks on B + all consumers
migrating; E blocks on C.

## Step 3 blast radius — sized

Done after a full call-site / drawer / registry / test scan of
`com.scaffold.entities`. Headline: **migration is fully self-contained
— zero consumers outside entities + bridge.** GraphFlow has its own
parallel `IVariableBag`; no other package imports
`Scaffold.Entities.IVariableBag` or variable types.

### Call-site counts

| API | Count | Distribution | Notes |
|---|---:|---|---|
| `IVariableBag.TryGetBase` | **21** | 12 tests + 9 runtime | Dominant rewrite target; survives as deprecated wrapper |
| `IVariableBag.LocalKeys` | 2 | EntityDefinition + LocalVariableStorage | Light |
| `IEntityVariableStorage.AddVariable` | 11 | EntityInstance, EntityComponentT, tests | Stays on entity surface |
| `IEntityVariableStorage.SetBaseValue` | 10 | same | Stays on entity surface |
| `IEntityVariableStorage.AddModifier` etc. | ~40 | EntityInstance, EntityComponentT, tests | **No migration** — modifier surface unchanged |
| `Variable` constructor | 12 (8 files) | Mostly tests + authoring (VariableSO, EntityModifierEntry) | Extraction-dependent |
| `VariableValue` references | 129 | Registry, drawers, polymorphic dispatch | Internal — proposal doesn't touch storage |
| `VariableValueRegistry` consumers | 7 files | Editor / authoring only | Stays private to entities |

### Drawer hub: `VariableKeySoField`

One file is the registry coupling point:
`Editor/VariableKeySoField.cs:83` calls
`VariableValueRegistry.TryResolve(payloadTypeId)`. **Every other
drawer reaches the registry through this file.** If `payloadTypeId`
ever renames or moves, this is the only place that breaks. Drawer
revalidation budget rides on this one file plus integration testing
of the four drawers that delegate through it
(`VariablePropertyDrawer`, `VariableBagPropertyDrawer`,
`EntityModifierEntryDrawer`, `VariableSOEditor`).

### Sized estimate

**Optimistic — `TryGetBase` survives as extension-method wrapper, no
test rewrites:**

- Shared package creation: 2d
- GraphFlow migration (Phase A): 1–2d
- Entities migration (Phase B): 2d (new `TryGet<T>` surface + light
  drawer revalidation + zero test rewrites)
- States bridge `StoreBackedVariableBag` + builder (Phase C): 3–4d

**Phase B alone: ~2 person-days. Total: ~7 person-days.**

**Pessimistic — full refactor, drop `TryGetBase`, rewrite all 21
call sites + 18 test sites:**

- Shared package creation: 2–3d
- GraphFlow migration: 2–3d
- Entities migration: 10–12d (3d call-site rewrites, 2d test
  rewrites, 1d `VariableKeySoField` registry rework, 2d drawer
  revalidation, 2d `Variable` extraction fallout)
- States bridge: 3–4d

**Phase B alone: ~10–12 person-days. Total: ~16 person-days.**

The swing factor between optimistic and pessimistic is **almost
entirely whether `TryGetBase` is kept as a deprecated wrapper.**
That's a single architectural decision; pick optimistic and the
migration is one developer-week.

### Phasing — A / B / C run independently

**Phase A** — `com.scaffold.variables` package + GraphFlow migration.
Self-contained; lands and ships independent of entities.

**Phase B** — Entities migration. Blocks on A landing. Internal to
entities + bridge. Drawer integration testing required.

**Phase C** — `StoreBackedVariableBag` + builder API in the bridge.
Net-new, additive. Blocks on B (needs the shared interfaces wired
through `IEntityVariableStorage`).

**A and B can run in parallel** if developer capacity allows — they
touch disjoint files. C must wait for B.

### Decisions still open before Phase B starts

1. **`Variable` ownership** — **DECIDED: shared package owns
   `Variable`.** Entity drops its own `Variable` class; the
   `payloadTypeId` field (entity's tag for the polymorphic
   `VariableValue` subclass — "int" / "float") moves to a separate
   concern (`VariableEntry` or registry-side), not onto shared
   `Variable`. Shared `Variable` carries `id` + `typeName` (the
   runtime `T`'s type identity, e.g. `System.Int32`). Cost: this is
   the more invasive Phase B path (~10–12d), but produces a clean
   abstraction with no cross-package smell.
2. **Bridge strategy** — **DECIDED: extension method, then erase.**
   Phase B adds `TryGet<T>` as the new surface and `[Obsolete]`
   extension methods routing `TryGetBase` → `TryGet<T>` so existing
   callers keep compiling. **Phase D (new):** after every internal
   caller migrates to `TryGet<T>`, the extension methods are
   deleted. Not a permanent compatibility shim — a transition.
3. **`VariableValueRegistry` shared interface** — defer per audit
   #1. Stays private to entities for v1.

### Phase B revised cost under decision #1

The "shared owns `Variable`" choice changes the audit numbers. The
~30 sites in entities that read `Variable.PayloadTypeId` ("int",
"float") need to either (a) keep that string somewhere else
(`VariableEntry.payloadTypeId`?) or (b) be rewritten to derive the
tag from the runtime `Type` via the registry. Drawer code in
`VariableKeySoField.cs:83` is the hub here.

Updated Phase B estimate: **~10–12 person-days** (was 2d optimistic).
Total end-to-end: **~16 person-days**. The extension-method-then-erase
strategy still helps — call sites can migrate gradually rather than
in one cut — but the `Variable` extraction itself is the load-bearing
cost.

## Binding builder — API design pressure-test

The original sketch hand-waved **how a consumer registers a
state-backed handle**. This is the user-facing API, so it has to be
worked out before Phase C lands. Pressure-testing three shapes
against realistic call sites.

### Shape 1 — fully generic

```csharp
var bag = new StoreVariableBagBuilder(store)
    .Bind<EntityState, float, SetBaseValuePayload>(
        variableId: hpGraphVarId,
        sliceRef:   heroRef,
        project:    s => s.GetBase<float>(hpVar),
        toPayload:  v => new SetBaseValuePayload(heroRef, hpVar, VariableValue.Of(v)))
    .Build();
```

**Pros:** one method covers every binding case; type-safe via
generics.

**Cons:** every entity-base binding repeats the same projector +
payload boilerplate. Three type parameters to write (or annotate
lambdas to enable inference). Awkward for the common case.

### Shape 2 — convenience overloads only

```csharp
.BindEntityBase<float>(hpGraphVarId, heroRef, hpVar)
.BindEntityComputed<float>(maxHpGraphVarId, heroRef, hpVar)
.BindEntityModifierStack(stacksGraphVarId, heroRef, hpVar)
```

**Pros:** dead simple for the entity case.

**Cons:** every new state shape needs a new `BindXxx` overload; the
generic case (raw state slices that aren't entities) has nowhere to
go.

### Shape 3 — chained `ForSlice<TState>` + typed `Bind` (recommended)

```csharp
var bag = new StoreVariableBagBuilder(store)
    // Entity bindings — shorthand from com.scaffold.entities.states
    .ForEntity(heroRef)
        .BindBase<float>(hpGraphVarId,    hpVar)
        .BindComputed<float>(maxHpGraphVarId, hpVar)
    // Generic slice projection — for non-entity state
    .ForSlice<TurnState>(gameRef)
        .Bind<int>(
            turnGraphVarId,
            project:   s => s.CurrentTurn,
            toPayload: v => new SetTurnPayload(v))
        .BindReadOnly<bool>(
            isPlayerTurnGraphVarId,
            project: s => s.ActorTag == "player")
    .Build();
```

**Pros:**
- Generic case (`Bind<T>` after `ForSlice<TState>`) is type-safe —
  `TState` fixed once, `T` inferred from `project`'s return,
  `TPayload` inferred from `toPayload`'s return. No ceremony.
- Entity case is one-liner via `ForEntity` shorthand from the bridge
  package — but reduces to the generic case underneath, no hidden
  paths.
- `BindReadOnly` covers aggregates and computed projections without
  needing a writable payload.
- Subscription dedup is automatic — multiple `Bind` calls under the
  same `ForSlice` share one `store.Subscribe<TState>(sliceRef)`.

**Cons:**
- Two layers of fluent builder (the outer + the per-slice scope).
- `ForEntity` lives in `com.scaffold.entities.states` (extends the
  shared builder); requires the bridge to be loaded for entity
  shorthand. Fine — entity bindings already need the bridge.

### Pressure-test: failure modes

| Failure | Caught at | Notes |
|---|---|---|
| Wrong projector for slice type (`s.NonExistentField`) | **Compile** | Lambda type-checked against `TState` |
| Wrong `T` for `BindEntityBase` (asks `<int>` for a float variable) | **Build()** | Entity registry knows `hpVar`'s `T`; validate during `Build()` and throw |
| Missing payload mutator registration (audit #6) | **Build()** | Builder records every `TPayload` used; checks each against `MutatorRegistry` at `Build()` |
| Slice doesn't exist at runtime (consumer forgot `RegisterSlice`) | **First Get** | Handle returns `default(T)` and warns; doesn't throw — consistent with graphflow's missing-cell behavior |
| `Set` on a `BindReadOnly` handle | **Compile** | Read-only handles return `IReadOnlyVariableHandle<T>`, not `IVariableHandle<T>` — no `Value` setter |
| Set re-entry from inside Subscribe callback | **Runtime** (suppressed) | Per-handle `_applyingFromSubscribe` guard, audit #5 |
| Cross-handle reentrancy cycle | **Runtime** (not caught) | Consumer responsibility; same as graphflow cells |

### Resolved questions on the builder

1. **Where does `StoreVariableBagBuilder` live?** **DECIDED:
   `com.scaffold.entities.states`.** Rationale: the bridge package
   becomes the canonical state-backed variable storage. Avoids
   creating a fourth sub-package. Tradeoff: consumers who want raw
   state-slice projection without entities still pull in the bridge
   — accepted, since entity-state-backed is the default path for
   variable storage going forward.
2. **Read-only return type.** **DECIDED: separate
   `IReadOnlyVariableHandle<T>` interface.** Compile-checked; costs
   one more interface in shared package. `IVariableHandle<T> :
   IReadOnlyVariableHandle<T>` so writable handles fit anywhere
   read-only is expected.
3. **Snapshot / save fidelity.** Bound handles dedupe on `_last`,
   so projection-equal commits skip `Subscribe` callbacks.
   **DECIDED test plan** (Q8 below): three-test fixture written
   before bridge code lands. (See Open questions Q8.)
4. **Disposable scope per `ForSlice`.** Per-variable unbinding
   without rebuilding the whole bag — **deferred**. Document the
   limitation; don't paint into a corner.

### Implication for the migration plan

Phase C is **single-phase** — `StoreVariableBagBuilder`,
`IReadOnlyVariableHandle<T>` consumer (the read-only handle
interface itself lives in shared `com.scaffold.variables`), and the
`ForEntity` / `ForSlice` builder API all land together in
`com.scaffold.entities.states`.

Order: A → B → C.

## Open questions

1. **Where does the `payloadTypeId` ↔ `System.Type` registry live?**
   Probably entity-owned, but graphflow needs a shape it can read.
   Cheapest: a small `IVariableTypeRegistry` interface in the shared
   package, with the default registry registered by entities at
   bootstrap.
2. **Does the shared package own `Variable` (the key) or only the
   bag/handle interfaces?** Owning `Variable` is more useful (entities
   stops being the source of truth) but is the most invasive
   migration. Alternative: shared package owns only `IVariableBag` /
   `IVariableHandle<T>` keyed by `string id`, and `Variable` stays in
   entities as a richer key type that wraps the string.
3. **Do we want a `[SerializeReference] List<RuntimeVariable>`
   serialization helper in the shared package?** GraphFlow has it;
   entities has its own (`VariableEntry`); states bridge has none. A
   shared serialization helper is tempting but locks the wire format.
   Probably defer.
4. ~~**Should `IVariableHandle<T>` expose `Subscribe(Action<T>)` /
   `Unsubscribe` instead of `event Action<T> Changed`?**~~ **DECIDED:
   method-style.** Lets `StoreBackedHandle` manage subscription
   bookkeeping (one store-side `Subscribe` fans out to many handle
   subscribers); makes teardown explicit, which the `IDisposable`
   bag needs anyway. In-memory handles wrap an internal event in a
   one-line `Subscribe` / `Unsubscribe` pair — no real cost.
5. **Naming.** `com.scaffold.variables` is boring and accurate.
   Resist "blackboard" — that's a *use* of the abstraction, not the
   abstraction itself.
6. **Where does `VariableValueRegistry` live, and does the shared
   package need an `IPayloadTypeRegistry` abstraction?** Entities
   owns the registry today (audit #1). Cross-package consumers can
   resolve through the existing entities surface; a shared interface
   is only worth adding if a third package actually needs the
   resolution. Likely defer.
7. **Should structural-change events be promoted to shared
   `IVariableBag`?** Argued no above (audit #3) — keep them
   entity-specific. Revisit if graphflow ever wants to react to
   bag-level adds/removes.
8. ~~**`StoreBackedHandle.Changed` semantics under deferral.**~~
   **DECIDED.** Contract: at most one Subscribe-callback per slice
   per `Execute` / snapshot load, with the final post-merge value.
   **Test plan locked** — three-test fixture lands before bridge
   code:
   1. Two `handle.Set(x)` writes inside one `Store.ExecuteBatch` →
      Subscribe-callback fires once with the final value.
   2. `Store.LoadSnapshot` that doesn't change the projected value
      → callback does NOT fire (handle dedupes on `_last`).
   3. `Store.LoadSnapshot` that changes the projected value →
      callback fires once with the new value, regardless of how
      many intermediate slice mutations the snapshot represents.
9. **Aggregate-backed handles — defer.** v1 documents canonical
   slices only (audit #7).
10. **Validation that all `StoreBackedHandle` payload types are
    registered with the dispatcher** (audit #6). Add to
    `EntityBridgeContext` bootstrap with a clear exception on
    missing registration.
11. **Binding builder API shape** — does the builder accept a
    `Variable` and infer payload factories via the entity registry,
    or always require explicit projector + factory? Decide before
    step 4 (Risk Triage gap #5).
12. **Migration mechanics for `IEntityVariableStorage : IVariableBag`**
    — duplicate `TryGetBase`, C# 8 default methods, or extension
    methods routing to `TryGet<T>`? Decide before step 3 (Risk
    Triage gap #3).

## Decision points before code lands

- ~~Confirm extraction~~ — **proceeding.**
- ~~Phase-B gate #1: `Variable` ownership~~ — **DECIDED: shared
  owns `Variable`.** Phase B revised to ~10–12d.
- ~~Phase-B gate #2: bridge strategy for
  `IEntityVariableStorage : IVariableBag`~~ — **DECIDED:
  extension-method wrapper, then erase** (Phase D added to migration
  plan).
- ~~Estimate entities migration blast radius~~ — **Done.** See
  "Step 3 blast radius — sized" section.
- ~~Decide Q4 — event vs. method-style subscription.~~ **DECIDED:
  method-style.**
- Decide Q6 — `IPayloadTypeRegistry` shared abstraction now, or
  defer. **Recommended: defer.**
- ~~Confirm `StoreBackedHandle.Changed` deferral semantics test
  plan (Q8)~~ **DECIDED: three-test fixture above.**
- ~~Pin builder open questions~~ **DECIDED:** builder lives in
  `com.scaffold.entities.states`; separate
  `IReadOnlyVariableHandle<T>` in shared package; per-variable
  unbinding deferred.

## Next steps

Three live paths forward:

- **(a) Start Phase A.** Create `com.scaffold.variables` (shared
  package) and migrate GraphFlow to it. Self-contained, lands
  independent of entities. ~3–5 person-days. Safest starting point —
  validates the abstraction shape against real consumer code before
  the bigger Phase B begins.

- **(b) Pin remaining open questions before any code.** Resolve
  Q4 (event vs. method-style subscription), Q11 (builder home
  package), Q12 (read-only handle interface), and the `Changed`
  deferral test plan (Q8). Cheap on the surface but has compounding
  effects — locking these now means Phase A code doesn't get
  rewritten when the answers come in. ~0.5 person-days of design
  work.

- **(c) Convert this design doc to a proper ExecPlan.** Phase
  breakdowns with definition-of-done per phase, snapshot harness
  expectations, test coverage targets, rollback plan if Phase B
  proves more invasive than estimated. The other plans under
  `Plans/GraphFlow/ExecPlan*.md` are the template. ~1 person-day.
  Recommended if multiple people will touch this; optional if
  one developer carries it through.

**Recommended path:** (b) → (a) → ExecPlan during Phase A → Phase B.
Pin the cheap-to-decide questions first, start the lowest-risk code
work, formalize the plan once Phase A reveals any surprises.
