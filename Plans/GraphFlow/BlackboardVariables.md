# Blackboard & Variables — Plan & Research

Status: **v1 implementation landed** — Phases 1–6 shipped behind PR #49
on `claude/graph-flow-blackboard-vars-IsUNX`. This doc remains the
design-of-record; deviations and deferrals are called out in the
phasing table and open-questions sections.

Companion follow-up to `ExecPlan-v2.md` §"v1 ships **without** a
designer-managed blackboard" — this doc resolves that v2 deferral.

v2 supersedes v1 on two points:

- **Three runtime layers** (flow / graph / parent), not two. Lookup
  cascades flow → graph → parent; Set finds the owning layer and
  writes there.
- **Typed cells** (`VariableCell<T>`) on the hot path, not `object?`.
  No boxing for `int` / `float` / `bool`; no `Type.GetField` /
  `Activator` / per-frame `is T` casts.

This doc captures the design direction for runtime blackboard /
variables in the GraphFlow package, the constraints that shape it, the
research that informs it, the implementation sketch (per layer), and
the open questions still to settle before code lands.

---

## Context

GraphFlow's edit-time canvas (Unity Graph Toolkit) already supports
declaring variables and dropping `IVariableNode` instances on the
canvas — it's "free" UI that comes with GT. At bake we currently
short-circuit those: a variable feeding a port is resolved to its
**default value as a literal** (`PortValueResolver.cs:18-19`,
`GraphBakerCore.cs:64`). Variable identity is dropped on the floor,
the runtime has no concept of a blackboard, and `Flow` has no variable
storage beyond `GetSlot/SetSlot` for transient per-node state.

A separate, parallel edit-time blackboard exists in
`com.scaffold.entities` (`VariableSO`, `VariableBag`, typed
`VariableValue<T>` registry, full property drawers). It is **not**
wired into GraphFlow today.

The goal of this plan is to make GraphFlow's runtime variable system
real, while keeping the package self-contained.

---

## Direction

Designed top-down, built bottom-up so the serialized contract is
pinned before the bake/run code that depends on it:

1. **Edit time** — GT's `IVariable` is the only authoring surface;
   designers declare variables in the GT blackboard panel and drop
   `IVariableNode`s on the canvas.
2. **Serialized form** — `GraphAsset` gains a typed
   `List<RuntimeVariable>` (declarations + defaults) and a
   `List<VariableEdge>` (port reads variable). Both use Unity's
   native `[SerializeReference]`, no JSON blob, no parallel object
   list.
3. **Bake** — translate GT variables into `RuntimeVariable[]`,
   translate `IVariableNode`-fed port edges into `VariableEdge[]`,
   stop short-circuiting to literals.
4. **Run — three-layer bag chain.** Lookup cascades closest → outermost,
   first match wins; writes go to the *owning* layer (the one that
   already declares the id), not the closest layer.

   | Layer | Owner | Lifetime | Seeded from |
   |---|---|---|---|
   | **Flow / instance** | `Flow.Variables` | Per `Run()` call | empty (or per-flow-declared subset; see Q7) |
   | **Graph** | `GraphRunner.Variables` | Per runner instance | `asset.variables` (RuntimeVariable[] defaults) |
   | **Parent / global** | `runner.CreateParentBag()` | Consumer-defined | Consumer-defined |

   `GraphBuilder.Build` constructs the graph-layer bag once at
   runner construction; `Flow` builds (or lazily allocates) its
   instance-layer bag at `Run()` time and chains it onto the
   runner's bag. Get/Set/Observe Variable nodes and
   `VariableEdge`-fed data ports read/write through `flow.Variables`.

The runtime stays fully self-contained — no dependency on
`com.scaffold.entities` or any other package. A consumer that wants
to bridge their own variable store (entities `VariableBag`, a save
system, a network-replicated store) implements `IVariableBag` and
returns it from `runner.CreateParentBag()`. That single seam is the
entire integration surface.

A second seam — exposing the same graph-layer fields to a plain C#
class passed at `Initialize` (FlowCanvas-style two-way binding) — is
**deferred**. It's a thin layer over the typed-cell `Changed` event
once that lands; no design choices today block it.

---

## Constraints

1. **Package independence.** GraphFlow runtime must not reference
   `com.scaffold.entities` or any consumer-specific type. All
   primitives (variable defaults, bag interface, in-memory bag) live
   in the GraphFlow runtime assembly.
2. **No JSON-blob serialization.** Both reference systems
   (FlowCanvas, Bolt) use FullSerializer + a `string` blob + parallel
   `List<UnityEngine.Object>` because they predate
   `[SerializeReference]`. We have SerializeReference; we use it.
3. **Zero new third-party dependencies.** No FullSerializer, no
   Newtonsoft, no AOT type registration plumbing.
4. **Runtime asset must be inspector-readable.** Variables, defaults,
   and edges are visible without custom property drawers (a
   `[SerializeReference]` polymorphic field renders natively in
   Unity).
5. **Stable identity = GUID.** Variables are referenced by GUID, not
   name. Renames must not break baked assets.
6. **Zero reflection on hot paths.** Lookups by variable id are
   dictionary-keyed; no per-frame `Type.GetField` or `Activator`.
7. **No regressions to the existing GT integration.** GT's
   `IVariable` remains read-only; we sample it at bake, never mutate
   it.

---

## Research summary

Three systems studied; their answers converge on the same *shape* but
differ on *encoding*.

### Unity Behavior Graph Toolkit (Graph Toolkit 0.4-exp)

What GT actually exposes (verified against the on-disk samples in
`Assets/Samples/Graph Toolkit/0.4.0-exp.2/` and Unity's API docs):

- `IVariable` — `name`, `dataType`, `Kind` (Local/Input/Output),
  `TryGetDefaultValue<T>(out T)`. Read-only.
- `IVariableNode.variable` — pointer to the `IVariable` for nodes
  dropped on canvas.
- `Graph.GetVariables()` — enumeration of declared variables.
- **No runtime namespace, no mutable API, no `BlackboardAsset`
  mirror, no port-side binding type.**

Both shipped samples (`TextureMaker`, `VisualNovelDirector`) treat
`IVariableNode` identically to `IConstantNode` —
`variable.TryGetDefaultValue<T>(out value)` and bake the result as a
literal. Unity's own positioning:

> "Graph Toolkit is a frontend framework focused on the authoring
> experience that supports compilation to runtime models but doesn't
> include runtime execution backends."

**Verdict:** GT is good for the authoring UI and for *one* read at
bake time. Runtime is 100% ours.

### Unity Behavior (`com.unity.behavior` 1.0)

Different package, different problem (behavior trees), but they ship
a real runtime blackboard:

- `[SerializeReference] BlackboardVariable<T>` declared as fields on
  custom action nodes.
- Authoring `BlackboardAsset` ↔ runtime `RuntimeBlackboardAsset`. The
  runtime asset creates per-instance copies of non-shared variables.
- `BehaviorGraphAgent.GetVariableValue<T>(name)` /
  `SetVariableValue` forwards to a `BlackboardReference`.
- Nodes use `[Serializable, GeneratePropertyBag,
  NodeDescription(...)]`.

**Takeaway:** confirms the per-port "either literal or bound to
variable" pattern, validates `[SerializeReference]` as the encoding,
and shows that an authoring/runtime asset split is workable.

### FlowCanvas / NodeCanvas (ParadoxNotion)

The most mature reference. Canonical `BBParameter<T>` design:

- Each node port-style field is a `BBParameter<T>` holding either a
  literal `_value` or a reference (`_targetVariableID` GUID + `_bb`
  blackboard) to a `Variable<T>` that lives in a `Blackboard`.
- On bind, `BBParameter` resolves once and caches getter/setter
  delegates; `.value` reads transparently.
- `Variable<T>` (`abstract Variable` + generic subclass) carries
  `_id`, `_name`, `_value`, optional `_propertyPath` for reflection
  binding to a component property/field.
- Three blackboard scopes: Graph (per-graph), Object
  (MonoBehaviour-on-agent), Global (asset). Blackboard parenting
  chains them.

Encoding:

```csharp
[SerializeField] string                    _serializedBlackboard;   // JSON
[SerializeField] List<UnityEngine.Object>  _objectReferences;       // hoisted
// OnBeforeSerialize → JSONSerializer.Serialize(BlackboardSource, ...)
// OnAfterDeserialize → JSONSerializer.Deserialize<BlackboardSource>(...)
```

`JSONSerializer` wraps **FullSerializer**. Object refs can't survive
JSON, so they're hoisted into a parallel `List<Object>` and re-stitched
by index. Deserialize-on-top-of-existing semantics so external refs
to `Variable<T>` instances stay valid.

### Unity Visual Scripting (Bolt) — `VariableDeclaration`

Same recipe, slightly less type-safe:

- `VariableDeclaration { string name; object value; }` — value is
  boxed `object`.
- Whole thing serialized via `Unity.VisualScripting.FullSerializer`.
- `AotList` (now obsolete) existed only to register types with the
  AOT compiler so IL2CPP wouldn't strip the JSON polymorphism types.
- Six scopes (Flow, Graph, Object, Scene, Application, Saved); pure
  string-keyed lookup, no per-port binding (Get/Set Variable nodes
  carry the strings).

### Why both went JSON-blob

These systems were designed before Unity had `[SerializeReference]`
(2019.3+). Unity's native serializer can't:

- Store polymorphic value types
- Serialize a `Dictionary` directly
- Round-trip an unknown user type without a known base class

JSON via FullSerializer solves all three. The cost: opaque in the
inspector, fragile across class renames, third-party dependency.

### Comparison table

| Aspect | FlowCanvas/Bolt (JSON blob) | GraphFlow (`SerializeReference`) |
|---|---|---|
| Wire format | Single `string` + `List<Object>` | Typed list of `RuntimeVariable` structs |
| Polymorphism | Any type | Known `VariableDefault` hierarchy |
| Inspector | Opaque w/o custom drawer | Native, free |
| Renames | Fragile (FullSerializer needs `[fsRecovery]`) | Robust (Unity handles via SerializeReference) |
| Extensibility | Open-ended (any user type) | Add a subclass of `VariableDefault` |
| Dep cost | Pulls in FullSerializer | Zero deps |
| AOT | Requires explicit type registration | SerializeReference is IL2CPP-safe |

---

## Architecture overview

```
┌────────────────────────────────────────────────────────────────────────┐
│  EDIT TIME                                                             │
│  • GT graph asset (separate file)                                      │
│  • IVariable declarations live in GT's blackboard panel                │
│  • IVariableNode instances dropped on canvas reference an IVariable    │
└──────────────────┬─────────────────────────────────────────────────────┘
                   │ bake
                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│  SERIALIZED (GraphAsset ScriptableObject)                              │
│  ── existing fields ──                                                 │
│  • [SerializeReference] List<RuntimeNode> nodes                        │
│  • List<Edge> connections        (data port → port)                    │
│  • List<Edge> flowEdges          (exec port → port)                    │
│  ── new fields ──                                                      │
│  • [SerializeReference] List<RuntimeVariable> variables                │
│      └─ id (GUID) + name + typeName + default value (typed subclass)   │
│  • List<VariableEdge> variableEdges                                    │
│      └─ variableId + toNodeId + toPortName                             │
└──────────────────┬─────────────────────────────────────────────────────┘
                   │ GraphBuilder.Build  (graph layer)
                   │ Run()               (flow layer)
                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│  RUN TIME — three-layer chain                                          │
│                                                                        │
│   flow.Variables  ──parent──►  runner.Variables  ──parent──►  parent   │
│   (per Run call)               (per runner inst)              (consumer)│
│                                                                        │
│  • IVariableBag                                                        │
│      Parent { get; }                                                   │
│      TryGetCell<T>(id, out VariableCell<T>)   ← typed, no boxing       │
│      Walk: local → parent → ... ; first match wins on Get;             │
│            owning layer wins on Set.                                   │
│  • VariableCell  (abstract base, key in dict)                          │
│  • VariableCell<T> { Id, T Value, event Action<T> Changed }            │
│      Storage is `T _value`, not `object`.                              │
│  • InMemoryVariableBag : IVariableBag                                  │
│      Dictionary<string /*id*/, VariableCell>                           │
│  • Flow.Variables   — instance-layer bag, parent = runner bag          │
│  • Runner.Variables — graph-layer bag, parent = CreateParentBag()      │
│  • VariableEdges  → DataBinding closures resolving cell at bake,       │
│                     OutputPort<T> closure reads cell.Value (no cast)   │
│  • Get/Set/Observe Variable nodes resolve cell once in Initialize,     │
│                     hot path is a typed field read/write              │
└────────────────────────────────────────────────────────────────────────┘
```

Single integration seam for consumers: implement `IVariableBag`,
return it from `runner.CreateParentBag()`. Done.

---

## Implementation sketch

Everything lives in
`Assets/Packages/com.scaffold.graphflow/Runtime/Variables/` (new
subfolder), except `RuntimeVariable` / `VariableEdge` which live next
to `Edge` on `GraphAsset`.

### 1. `VariableDefault` hierarchy (asset-side defaults)

The default-value record carries enough type information to build a
`VariableCell<T>` directly — no `Type.GetType` lookup at runtime, no
boxing.

```csharp
namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class VariableDefault
    {
        public abstract Type ValueType { get; }
        // Builds the typed cell for this default. Implementations are
        // one-liners — `new VariableCell<T>(id, value)` — so no
        // reflection, no boxing.
        public abstract VariableCell CreateCell(string id);
    }

    [Serializable]
    public abstract class VariableDefault<T> : VariableDefault
    {
        public T value = default!;
        public sealed override Type ValueType => typeof(T);
        public sealed override VariableCell CreateCell(string id)
            => new VariableCell<T>(id, value);
    }

    [Serializable] public sealed class IntDefault    : VariableDefault<int>    { }
    [Serializable] public sealed class FloatDefault  : VariableDefault<float>  { }
    [Serializable] public sealed class BoolDefault   : VariableDefault<bool>   { }
    [Serializable] public sealed class StringDefault : VariableDefault<string> { }
    [Serializable] public sealed class ObjectDefault : VariableDefault<UnityEngine.Object?> { }
    // Extension point: consumers add subclasses; Unity SerializeReference
    // discovers them automatically as long as they're [Serializable] and
    // visible to the editor at authoring time.
}
```

Initial set covers ~95% of designer-authored variables. See
**Open Questions §1** for the bigger list.

### 2. Serialized records — `RuntimeVariable`, `VariableEdge`

```csharp
namespace Scaffold.GraphFlow
{
    [Serializable]
    public struct RuntimeVariable
    {
        public string id;          // GUID, stable across renames
        public string name;        // human label, debug only
        public string typeName;    // System.Type.AssemblyQualifiedName
        [SerializeReference] public VariableDefault defaultValue;
    }

    [Serializable]
    public struct VariableEdge
    {
        public string variableId;
        public int    toNodeId;
        public string toPortName;
    }
}
```

Added to `GraphAsset` (extending `Assets/Packages/com.scaffold.graphflow/Runtime/Asset/GraphAsset.cs`):

```csharp
public List<RuntimeVariable> variables    = new();
public List<VariableEdge>    variableEdges = new();
```

Old assets without these fields deserialize as empty lists (Unity's
default), so this is non-breaking.

### 3. Runtime bag interface + cell

Typed cells avoid boxing on the hot path. The non-generic `VariableCell`
base exists only as a dictionary value type and an upcast bridge — every
read/write goes through `VariableCell<T>` directly.

```csharp
namespace Scaffold.GraphFlow
{
    public interface IVariableBag
    {
        IVariableBag? Parent { get; }

        // Returns the typed cell for `id` from this bag or any parent.
        // Callers (Get/Set nodes, port-bind closures) cache the cell at
        // Initialize / bake time; hot path becomes a direct field read.
        bool TryGetCell<T>(string id, out VariableCell<T> cell);

        // Used by the bag chain itself (and by introspection / save).
        // Most call sites should use TryGetCell<T>.
        bool TryGetCell(string id, out VariableCell cell);
    }

    public abstract class VariableCell
    {
        public string Id   { get; }
        public Type   Type { get; }
        protected VariableCell(string id, Type type) { Id = id; Type = type; }
    }

    public sealed class VariableCell<T> : VariableCell
    {
        T _value;
        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                _value = value;
                Changed?.Invoke(value);
            }
        }

        public event Action<T>? Changed;

        public VariableCell(string id, T initial)
            : base(id, typeof(T)) { _value = initial; }
    }
}
```

Notes:

- `EqualityComparer<T>.Default.Equals` does no boxing for value types
  and resolves to the `IEquatable<T>` implementation when available.
- The non-generic `TryGetCell` exists for introspection (inspector,
  save/load); it's not used on the hot path. `TryGetCell<T>` itself
  does one `raw is VariableCell<T>` pattern match per call to bridge
  the dictionary's non-generic value type to the typed cell — no
  boxing, no reflection. Hot-path callers (Get/Set/Observe nodes,
  variable-bound port closures) call `TryGetCell<T>` exactly once at
  Initialize / bake time and cache the resulting `VariableCell<T>`
  reference, so steady-state reads/writes skip even that pattern
  match.

### 4. In-memory bag (default implementation)

```csharp
public sealed class InMemoryVariableBag : IVariableBag
{
    readonly Dictionary<string, VariableCell> _cells = new();
    public IVariableBag? Parent { get; }

    public InMemoryVariableBag(IEnumerable<RuntimeVariable> seed,
                               IVariableBag? parent = null)
    {
        Parent = parent;
        foreach (var v in seed)
        {
            // Each RuntimeVariable's defaultValue is a typed subclass
            // (IntDefault : VariableDefault<int>, ...). It builds its own
            // VariableCell<T> — no boxing, no Type.GetType lookup at runtime.
            _cells[v.id] = v.defaultValue!.CreateCell(v.id);
        }
    }

    public bool TryGetCell<T>(string id, out VariableCell<T> cell)
    {
        if (_cells.TryGetValue(id, out var raw))
        {
            if (raw is VariableCell<T> typed) { cell = typed; return true; }
            cell = null!;                       // declared at wrong type
            return false;
        }
        if (Parent != null) return Parent.TryGetCell<T>(id, out cell);
        cell = null!;
        return false;
    }

    public bool TryGetCell(string id, out VariableCell cell)
    {
        if (_cells.TryGetValue(id, out cell!)) return true;
        if (Parent != null) return Parent.TryGetCell(id, out cell);
        cell = null!;
        return false;
    }
}
```

Lookup cascade: local → parent → ... → null. **Writes target the
*owning* bag** — i.e. the bag whose `_cells` already contains the id.
Because callers cache the resolved `VariableCell<T>` reference, the
bag identity is implicit: `cell.Value = x` writes wherever the cell
lives, so bubble-up is automatic and free.

The flow-layer bag is a thin `InMemoryVariableBag` with no seeds —
its only job is to optionally hold per-flow declarations (Q7) and to
chain to the runner bag.

### 5. Runner + Flow integration (three-layer chain)

The graph-layer bag lives on `GraphRunner` (one per runner instance,
seeded from `asset.variables` once). The flow-layer bag lives on
`Flow` (one per `Run()` call, parent = runner's bag).

```csharp
public abstract class GraphRunner
{
    // ... existing members ...

    public IVariableBag Variables { get; private set; } = null!;

    /// <summary>Override to inject the outermost (parent / global) bag —
    /// shared variables, save state, network store, entities VariableBag
    /// adapter, ... GraphFlow runtime never references the consumer's types.</summary>
    protected internal virtual IVariableBag? CreateParentBag() => null;

    internal void SeedVariables(IReadOnlyList<RuntimeVariable> seed)
    {
        // Called once from GraphBuilder.Build, before user Initialize().
        Variables = new InMemoryVariableBag(seed, CreateParentBag());
    }
}

public sealed class Flow
{
    // ... existing fields ...
    public IVariableBag Variables { get; }

    internal Flow(object payload, GraphRunner runner, CancellationToken token)
    {
        // ... existing wiring ...
        Variables = new InMemoryVariableBag(Array.Empty<RuntimeVariable>(),
                                            parent: runner.Variables);
    }
}
```

Lookup walked from `flow.Variables`: flow → runner → parent. Sets land
on whichever layer owns the cell (since callers cache the
`VariableCell<T>` ref, "owns" is implicit). Flow-layer declarations
(per-execution scratch vars) are out of v1 scope — see Q7.

The `SeedVariables` plumbing is internal; consumers only see
`runner.Variables` (read) and `CreateParentBag()` (override).

### 7. Variable-bound input ports

When the source of an input port is an `IVariableNode`, the bake
records a `VariableEdge`. At graph build time we resolve the
`VariableCell<T>` once and produce an `OutputPort<T>` whose closure
reads the cached cell — zero dictionary lookup, zero cast on the
hot path:

```csharp
// Built once per VariableEdge in GraphTopology.Bake (or sibling).
if (!runner.Variables.TryGetCell<T>(edge.variableId, out var cell))
    throw new InvalidOperationException(
        $"Variable {edge.variableId} not declared / type mismatch.");

var output = new OutputPort<T>(_ => cell.Value, cache: false);
input.Connect(output);
```

Caveat: caching the cell at graph-build time means a parent-bag swap
mid-run is invisible to already-bound ports. That's acceptable for
v1 (consumers swap parents at runner construction, not mid-flow); see
Q3 if per-flow parents become a real need.

### 8. Bake — what changes in the editor

- `GraphBakerCore.cs:64` — stop skipping `IVariableNode`. Variables
  now matter at bake.
- New step in the bake pipeline: walk `graph.GetVariables()`, emit
  `RuntimeVariable[]`. Default value comes from
  `IVariable.TryGetDefaultValue<T>` boxed into the matching
  `VariableDefault` subclass via a small `TypedDefault.For(type, value)`
  helper.
- Variable identity (GUID) — GT exposes only `Name`. We need to fish
  a stable GUID out via the `EditorNodeIdentity`-style reflection
  trick already used for nodes (`EditorNodeIdentity.cs:1-30`), or
  hash the name as a fallback if reflection access ever breaks.
- Edge translation: when iterating data edges, if the `from` side is
  an `IVariableNode`, emit a `VariableEdge` (with `variableId` =
  the variable's GUID) instead of a `connections` Edge. Otherwise
  unchanged.
- `PortValueResolver.cs:18-19` — keep the `IVariableNode` branch for
  callers that genuinely want the *default* (e.g. a designer
  preview), but the main bake path no longer routes through it.

### 9. Get / Set / Observe Variable runtime nodes

These are normal runtime nodes; they cache the typed cell once in
`Initialize` and then read/write it directly:

```csharp
[GraphNode(Category = "Variables")]
public sealed partial class GetVariableNode<T> : RuntimeNode
{
    [SerializeField] string variableId = string.Empty;
    public OutputPort<T> Value = null!;

    VariableCell<T>? _cell;

    public override void Initialize(GraphRunner runner)
        => runner.Variables.TryGetCell<T>(variableId, out _cell!);

    partial void InitializePorts() =>
        Value = new OutputPort<T>(_ => _cell != null ? _cell.Value : default!,
                                  cache: false);
}

[GraphNode(Category = "Variables")]
public sealed partial class SetVariableNode<T> : RuntimeNode
{
    [SerializeField] string variableId = string.Empty;
    public InputPort<T>  NewValue = null!;
    public FlowInPort    In       = null!;
    public FlowOutPort   Done     = null!;

    VariableCell<T>? _cell;

    public override void Initialize(GraphRunner runner)
        => runner.Variables.TryGetCell<T>(variableId, out _cell!);

    partial void InitializePorts() =>
        In = FlowInPort.Sync(this, nameof(In), flow =>
        {
            if (_cell != null) _cell.Value = NewValue.Read(flow);
            return Done;
        });
}
```

Hot path is `_cell.Value = x` / `_ => _cell.Value`. No dictionary
lookup per Run, no boxing, no cast.

`Observe` (event-on-change) caches the same `VariableCell<T>`,
subscribes to `cell.Changed` in `OnFlowStart`, unsubscribes in
`OnFlowEnd`. The handler is `Action<T>`, again no boxing.

The source generator (`GraphPackageTrioEmitter`) emits one Get/Set/
Observe trio per registered `VariableDefault<T>` subclass — same
per-type-emission machinery already used for typed ports.

---

## Phasing

Build green at every commit, mirror the M3 / Runner-Redesign
discipline. Suggested ordering:

| Phase | Lands | Touches | Status |
|---|---|---|---|
| 1 | `VariableCell` / `VariableCell<T>`, `IVariableBag`, `InMemoryVariableBag`, `VariableDefault`/`VariableDefault<T>` + initial 5 subclasses, `RuntimeVariable`, `VariableEdge`. Schema bump on `GraphAsset` (empty lists, non-breaking). | Runtime | shipped |
| 2 | Three-layer chain wired: `GraphRunner.Variables` seeded by `GraphBuilder` from `asset.variables` with `CreateParentBag()` parent; `Flow.Variables` chained on top per `Run()`. | Runtime | shipped |
| 3 | Bake-time emission of `RuntimeVariable[]`; stop skipping `IVariableNode`; emit `VariableEdge[]`; cell-cached `OutputPort<T>` for variable-bound input ports. | Editor + Runtime | shipped |
| 4 | `GetVariableNode<T>` / `SetVariableNode<T>` for int / float / bool / string / Object. Hand-authored typed concretes — generator-driven trio emission deferred until a real consumer asks. | Runtime | shipped |
| 5 | `ObserveVariableNode<T>` + cell `Changed` events. New `internal RunObserver` seam on `GraphRunner`. Lifecycle (subscription teardown) deferred. | Runtime | shipped |
| 6 | End-to-end test (`VariableEndToEndTests`) exercising graph-level + global vars, variable-bound ports, Get/Set/Observe in one graph. Real GT-authored sample asset still pending Unity. | Tests | shipped (test-only) |
| later | External-class two-way binding (FlowCanvas-style passed-in object exposing the same fields, kept in sync via cell `Changed`). | Runtime |
| later | Per-flow declarations + subgraph parameters (Q7). | Bake + Runtime |
| later | Source-generator emission of Get/Set/Observe trios per registered `VariableDefault<T>` — replaces the hand-authored concretes once a consumer needs additional types. | Generator |
| later | Editor variable-picker drawer for the `variableId` `[SerializeField] string` on Get/Set/Observe nodes. | Editor |
| later | Subscription teardown for Observe nodes that target parent-bag cells. | Runtime |

Each phase ends with the snapshot harness green and Unity batchmode
compile zero `error CS` lines. Phases 1+2 are trivially independent
of GT; phase 3 is the only one touching GT internals.

---

## Post-v1 follow-up backlog

Captured after the implementation landed. Organized by priority — the
"likely-to-bite" items are real bugs or hardening gaps that a real
consumer is most likely to hit; the rest is deferred-but-known scope.

### Likely to bite (fix soon)

1. **Multi-runner footgun.** `GraphBuilder` caches `BakedGraph` per
   asset and `WireVariableEdges` mutates the cached `RuntimeNode`
   ports in place. Two runners off the same builder over the same
   asset → second `Build` rewires the first runner's ports. Same
   constraint already applies to Get/Set/Observe `Initialize` cell
   caching. Fix: per-`Build` cloning of node port state, or stop
   caching `BakedGraph`. Documented at `GraphBuilder.cs:26-31`.
2. **No bake-time type validation on `VariableEdge`.** Designer wires
   a `string` `IVariableNode` to an `int` `InputPort` → bake accepts,
   runtime silently fails (`TryGetCell<int>` returns false on the
   wrong type, port reads `default(T)`). The information is in GT
   (`port.dataType` + `variable.dataType`); add an early `LogError`
   in `BakeVariables`.
3. **Better DX for unsupported `VariableDefault` types.** Today
   `EditorVariableDefaults.CreateFor` only knows about
   int/float/bool/string/Object; anything else surfaces a generic
   "unsupported dataType" bake error. Consumers can author a
   `VariableDefault<Vector3>` runtime-side, but the *editor* mapping
   doesn't auto-pick it up — there's no registry. Either add a
   reflection-driven discovery pass (find all `VariableDefault<T>`
   subclasses and map by `T`) or make consumers register.
4. **`RuntimeVariable.typeName` is descriptive-only and can drift.**
   Runtime uses `defaultValue.ValueType` to seed the cell; `typeName`
   isn't load-bearing. Drop it, or assert at bake that
   `typeName == defaultValue.ValueType.AssemblyQualifiedName`.
5. **No introspection API on `IVariableBag`.** Inspector views,
   save/load, debug tooling all need an `Enumerate()` / `Cells`
   surface. `TryGetCell` is keyed lookup only. Add when one of those
   features actually lands.
6. **Identity-drift posture mismatch.** The merged-in
   `EditorNodeIdentity` hard-fails on reflection drift (returns
   null, baker errors). `EditorVariableIdentity` falls back to
   `"name:" + variable.name`. Pick one — either both hard-fail or
   both fall back. Hard-fail is safer (rename-fragile fallback can
   silently break baked assets).

### Test-coverage gaps

7. **Zero coverage on the editor bake path.** `EditorVariableIdentity`,
   `EditorVariableDefaults`, `BakeVariables` are entirely untested —
   they need a real GT graph. The casing bug (`Name` → `name`) that
   landed only post-Unity is exactly the class of issue editor tests
   would catch. At minimum: one round-trip test that authors a graph
   in code (using GT's editor API), bakes it, and asserts the
   resulting `RuntimeVariable[]`.
8. **No cycle-detection test on the parent bag chain.** Q9 — trivial
   to add a visited set if a real consumer ever wires a cyclic
   parent.
9. **No test for unconnected variable-bound port.** When `variableId`
   doesn't resolve, the builder logs a warning and leaves the port
   reading `default(T)`. Behavior is intentional but untested.
10. **No re-bake regression test.** Author → bake → rename a
    variable in the GT blackboard → re-bake. Should preserve baked
    edges (GUID-based identity) or surface a clean error. Untested.
11. **No multi-`Build` test.** Calling `Build` on the same asset
    twice with the same builder — does anything corrupt? See item
    #1.

### Deferred (already-known scope)

12. **External-class two-way binding.** FlowCanvas-style
    consumer-supplied object exposing the same fields, kept in sync
    via `cell.Changed`. The lesser-priority item from the original
    ask. Slottable cleanly when wanted.
13. **Generator-driven Get/Set/Observe trio emission per
    `VariableDefault<T>`.** v1 ships hand-authored 15 typed
    concretes. Adding a new `T` is one subclass + one trio + meta
    file × 3. Generator emission would replace the hand-authored
    set, no runtime change.
14. **Editor variable-id picker drawer.** `variableId` is a plain
    `[SerializeField] string` — designers paste GUIDs.
15. **Observe subscription teardown.** Subscriptions to parent-bag
    cells outlive the runner, leaking a runner-keepalive. Add an
    explicit teardown / `IDisposable` on `GraphRunner` when it
    matters.
16. **Per-flow variable declarations (Q7).** Flow-layer bag is
    structurally present but always empty. When a real designer
    feature asks for "scratch state per Run," tag variables with a
    scope enum at bake and route flow-scoped ones into
    `flow.Variables`.
17. **Subgraph parameters (Q7).** GT's `IVariable.Kind`
    (Local/Input/Output) is currently ignored. When subgraphs land,
    Input/Output become subgraph parameters and naturally live on
    the per-call flow bag.
18. **Real GT-authored sample asset.** Phase 6's "sample" is the
    `VariableEndToEndTests` integration test — same code paths,
    no `.asset` on disk. Needs Unity to author.

### Behavioral quirks worth knowing (not bugs)

- **Type-mismatch shadows the parent.** Child bag declaring `hp` at
  the wrong type fully shadows the parent's `hp`, by design. Comment
  in `InMemoryVariableBag.TryGetCell<T>` documents.
- **`Object` cells can hold null.** `VariableCell<UnityEngine.Object>`
  null values are valid; `GetObjectVariable` returns `null!`
  (forgiving) in that case.
- **`Flow.Variables` allocates per `Run()` if touched.** Lazy — only
  pays when accessed. High-frequency runners that touch variables
  every frame eat one allocation each run. Acceptable for v1; pool
  or skip flow layer if it ever shows up in profiles.

---

## Open questions / concerns / gaps

### 1. Initial `VariableDefault` set

Proposed minimum: `Int`, `Float`, `Bool`, `String`, `Object` (= 5).
Likely worth adding day-one: `Vector2`, `Vector3`, `Color`. Maybe:
`Long`, `Double`, `Enum<T>` (tricky — `[SerializeReference]` vs.
generic interactions). Anything beyond that probably waits for a
real use case.

**Decision needed before code lands** — locks the wire format.

### 2. Variable identity — GUID source

GT's `IVariable` exposes `name` and `dataType` publicly but not a
GUID. We have two options:

- **Reflection on the GT model implementation field** — same trick
  `EditorNodeIdentity.cs` already uses for nodes. Stable across GT
  versions so far but unsupported.
- **Hash the name** — simple, but breaks on rename.

Reflection is the better default; fall back to hash + warn if the
reflection path ever breaks under a GT update. Worth confirming the
field is reachable in 0.4-exp before phase 3.

### 3. `CreateParentBag()` lifetime

`protected internal virtual` on `GraphRunner`, called once at
`SeedVariables` time (graph-layer construction). Per-runner only.

Per-flow parent injection was considered and **rejected for v1**:
caching `VariableCell<T>` references at graph build time means a
mid-run parent swap is invisible to already-bound ports anyway. If a
real use case demands per-flow parents, the typed-cell cache becomes
the wrong abstraction and we'd need to rethink the bind step. Don't
pay for that flexibility until something asks for it.

### 4. Custom user types — escape hatch policy

Typed defaults cover primitives + Object. For genuinely custom
types (a designer-authored struct), choices are:

- **Force users to define their own `VariableDefault` subclass.**
  Clean, explicit, no JSON.
- **Provide a `JsonDefault { string typeName; string json; }` escape
  hatch.** Lets `JsonUtility` carry arbitrary `[Serializable]` types.

Recommend skipping `JsonDefault` until a real use case appears.
`[SerializeReference]` + a one-line subclass is so cheap that it's
not worth the JSON path's downsides.

### 5. Write semantics across the parent chain

Resolved: bubble-up, free of charge. Because Get/Set/port-bind callers
cache the resolved `VariableCell<T>` reference at Initialize / bake
time, "write to the owning layer" is implicit — `cell.Value = x`
hits whichever bag's dict the cell lives in. There's no need for a
per-Set lookup or an explicit owner check.

A "shadow locally" mode (Python `nonlocal`-style scratch override on
the closer layer) is **deferred**. It's a real feature only once
flow-layer declarations exist (Q7); until then the flow bag is empty
and shadowing has nothing to shadow into.

### 6. Read fallback when type mismatches

Typed cells turn this into a bake-time question, not a runtime one:

- `TryGetCell<T>` returns false if the cell exists at the wrong
  type (`raw is VariableCell<T>` fails). The Get node returns
  `default(T)`; the variable-bound port-bind throws at build time.
- Polymorphism (a `GameObject` variable read as a `Component`) does
  **not** work transparently — `VariableCell<GameObject>` is not a
  `VariableCell<Component>`. If a real use case appears, add an
  upcasting wrapper cell (cheap, opt-in) rather than re-boxing every
  read.
- Designer mistakes (binding an `int` port to a `float` variable)
  should be caught at bake — both sides have GT-side type info. Add
  validation in the bake step; runtime stays strict.

### 7. Per-flow declarations and subgraphs

The flow-layer bag exists structurally (chain head) but is empty in
v1. Two follow-on features will populate it:

- **Per-flow scratch variables.** A designer-authored "flow var"
  scope, declared on the graph but seeded fresh per `Run()`. Useful
  for accumulator state inside a single execution. Trivial: tag
  variables with a scope enum at bake, route flow-scoped ones into
  `flow.Variables` instead of `runner.Variables`.
- **Subgraphs.** GT's `IVariable.Kind` (Local/Input/Output) becomes
  meaningful — Input/Output are subgraph parameters and naturally
  live on the per-call flow bag. Out of scope here; flag for the
  subgraph plan.

Both fit cleanly because the bag chain already exists; adding them
does not change the v1 wire format.

### 8. Save / load

FlowCanvas's `BlackboardSource` is JSON-serializable for save games.
Our `InMemoryVariableBag` isn't directly persistence-ready — values
live in typed `VariableCell<T>` instances keyed by `string` id, which
needs a small per-type (de)serializer. Two paths when this comes up:

- **Snapshot via `RuntimeVariable[]` round-trip** — cheap if all
  cells map to known `VariableDefault` subclasses; lossy for unknown
  types.
- **Delegate to consumer** — bag adapter implements its own
  persistence (entities `VariableBag` already has this).

Defer. Most consumer use cases will go through the adapter path
anyway.

### 9. Parented bag ordering

If a consumer's parent bag itself has a parent, the chain walks
correctly. But there's no cycle detection. Trivial to add (visited
set in `TryGetCell<T>` / `TryGetCell`); cheap to skip in the v1
sketch. Add when adding sample tests.

### 10. `IVariableNode` editor preview

Today `PortValueResolver` resolves variables to their default for
editor previews. After the bake change, the bake path no longer goes
through it — but standalone preview / inspector tooling still might.
Keep `PortValueResolver` as-is; treat it as a read-only utility.

---

## Sources

- [Unity Behavior — Blackboard variables](https://docs.unity3d.com/Packages/com.unity.behavior@1.0/manual/blackboard-variables.html)
- [Unity Behavior — Interact via C#](https://docs.unity3d.com/Packages/com.unity.behavior@1.0/manual/bind-c.html)
- [Unity Behavior — BlackboardReference API](https://docs.unity3d.com/Packages/com.unity.behavior@1.0/api/Unity.Behavior.BlackboardReference.html)
- [IVariable interface (Graph Toolkit 0.1)](https://docs.unity3d.com/Packages/com.unity.graphtoolkit@0.1/api/Unity.GraphToolkit.Editor.IVariable.html)
- [Graph Toolkit introduction](https://docs.unity3d.com/Packages/com.unity.graphtoolkit@0.1/manual/introduction.html)
- [GraphToolkit feedback thread](https://discussions.unity.com/t/graphtoolkit-feedback-on-the-api-and-ui/1683123)
- [FlowCanvas Blackboard.cs (mirror)](https://github.com/ihaiucom/learn.FlowCanvas/blob/master/UnityGame/Assets/ParadoxNotion/FlowCanvas/Framework/Runtime/Variables/Blackboard.cs)
- [FlowCanvas Variable.cs (mirror)](https://github.com/ihaiucom/learn.FlowCanvas/blob/master/UnityGame/Assets/ParadoxNotion/FlowCanvas/Framework/Runtime/Variables/Variable.cs)
- [FlowCanvas BBParameter.cs (mirror)](https://github.com/ihaiucom/learn.FlowCanvas/blob/master/UnityGame/Assets/ParadoxNotion/FlowCanvas/Framework/Runtime/Variables/BBParameter.cs)
- [NodeCanvas data binding](https://nodecanvas.paradoxnotion.com/documentation/?section=data-binding-variables)
- [VariableDeclaration — Visual Scripting 1.7](https://docs.unity3d.com/Packages/com.unity.visualscripting@1.7/api/Unity.VisualScripting.VariableDeclaration.html)
- [VariableDeclarations — Visual Scripting 1.7](https://docs.unity3d.com/Packages/com.unity.visualscripting@1.7/api/Unity.VisualScripting.VariableDeclarations.html)
- [Unity.VisualScripting.FullSerializer namespace](https://docs.unity3d.com/Packages/com.unity.visualscripting@1.9/api/Unity.VisualScripting.FullSerializer.html)
- [Unity Visual Scripting variable scopes](https://docs.unity3d.com/Packages/com.unity.visualscripting@1.9/manual/vs-variables.html)
- Local repo references:
  - `Plans/GraphFlow/ExecPlan-v2.md` (v1 deferral note)
  - `Assets/Packages/com.scaffold.graphflow/Editor/PortValueResolver.cs:1-24`
  - `Assets/Packages/com.scaffold.graphflow/Editor/GraphBakerCore.cs:64`
  - `Assets/Packages/com.scaffold.graphflow/Runtime/Asset/GraphAsset.cs:1-39`
  - `Assets/Packages/com.scaffold.graphflow/Runtime/Builder/NodeBuildSlice.cs:1-40`
  - `Assets/Packages/com.scaffold.graphflow/Runtime/Flow/Flow.cs:62-66` (slot system)
  - `Assets/Samples/Graph Toolkit/0.4.0-exp.2/TextureMaker Sample/Editor/TextureMakerGraph.cs:93-118`
  - `Assets/Samples/Graph Toolkit/0.4.0-exp.2/VisualNovelDirector Sample/Editor/AssetImport/VisualNovelDirectorImporter.cs:149-176`
