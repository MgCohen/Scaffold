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
   constraint. Single integration seam: `runner.CreateParentBag()`
   returns an `IVariableBag?`.
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

    public interface IVariableHandle<T> : IVariableHandle
    {
        T Value { get; set; }
        event Action<T> Changed;
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

### 1. GraphFlow blackboard — low cost

Mostly renames. The existing design *already* matches the proposed
shape — `VariableCell<T>` *is* `InMemoryHandle<T>`; the existing
`IVariableBag` *is* the proposed one minus the namespace.

- `runner.CreateParentBag()` returns `Scaffold.Variables.IVariableBag?`.
- `RuntimeVariable.defaultValue` references shared `VariableDefault`.
- `InMemoryVariableBag`, `VariableCell<T>`, `VariableDefault<T>` move
  (or re-export) to the shared package.
- Zero-boxing preserved — graph-layer bag stays `InMemoryVariableBag`
  with field-typed storage. Only consumer-supplied parent bags are
  allowed to box.

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
    T    _last;
    bool _applyingFromSubscribe;

    public T Value
    {
        get => _project(_store.Get<TState>(_ref));
        set
        {
            if (_applyingFromSubscribe) return;     // re-entry guard
            if (EqualityComparer<T>.Default.Equals(_last, value)) return;
            _store.Execute(_ref, _toPayload(value));
        }
    }

    public event Action<T>? Changed;

    void OnSliceChanged(TState s, StateChangeEvent _)
    {
        var next = _project(s);
        if (EqualityComparer<T>.Default.Equals(_last, next)) return;
        _last = next;
        _applyingFromSubscribe = true;
        try { Changed?.Invoke(next); }
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

## Migration order (to keep green)

1. **Land `com.scaffold.variables`** with `Variable`,
   `IVariableHandle<T>`, `IVariableBag`, `VariableDefault<T>`,
   `InMemoryHandle<T>`, `InMemoryVariableBag`. Pure additive — no
   consumer changes yet.
2. **Migrate GraphFlow.** Replace its private types with re-exports /
   type-aliases to the shared ones. Tests stay green because the
   shapes are identical.
3. **Migrate entities.** `IEntityVariableStorage : IVariableBag`
   (shared). Add `TryGet<T>` to `VariableBag`. Existing `TryGetBase`
   callers keep working during the transition (deprecation, not
   removal).
4. **Add `StoreBackedVariableBag` to the states bridge.** Net-new
   code, no migration.
5. **Wire blackboard ↔ entity / state** via `runner.CreateParentBag()`
   returning a chained or composite shared `IVariableBag`.

Each step ends green; no consumer must adopt the new shape until its
own step lands.

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
4. **Should `IVariableHandle<T>` expose `Subscribe(Action<T>)` /
   `Unsubscribe` instead of `event Action<T> Changed`?** Method-style
   gives the implementation control over subscription bookkeeping
   (important for `StoreBackedHandle`, which holds a single store-side
   subscription that fans out). Event-style matches GraphFlow's
   existing `VariableCell<T>`. Likely method-style; one-line wrapper
   around an internal event for in-memory handles.
5. **Naming.** `com.scaffold.variables` is boring and accurate.
   Resist "blackboard" — that's a *use* of the abstraction, not the
   abstraction itself.

## Decision points before code lands

- Confirm extraction (vs. status quo with three parallel systems).
- Decide Q2 above — does shared own `Variable`, or only the bag/handle
  interfaces?
- Decide Q4 above — event vs. method-style subscription.
- Estimate the entities migration's blast radius (drawers + tests +
  `EntityState` embedding) before committing.
