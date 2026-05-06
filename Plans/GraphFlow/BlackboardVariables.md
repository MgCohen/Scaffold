# Blackboard & Variables — Plan & Research

Status: **Draft v1 — research consolidated, design shape locked, ready
to iterate on details.** Companion follow-up to `ExecPlan-v2.md` §"v1
ships **without** a designer-managed blackboard" — this doc resolves
that v2 deferral.

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

A four-layer split — designed top-down, but built bottom-up so the
serialized contract is pinned before the bake/run code that depends
on it:

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
4. **Run** — `GraphBuilder` constructs an `InMemoryVariableBag` of
   mutable `VariableCell`s, seeded from `RuntimeVariable` defaults,
   parented to a consumer-supplied bag chain. `Flow.Variables`
   exposes it. Get/Set Variable nodes and `VariableEdge`-fed data
   ports read/write through the bag.

The runtime stays fully self-contained — no dependency on
`com.scaffold.entities` or any other package. A consumer that wants
to bridge their own variable store (entities `VariableBag`, a save
system, a network-replicated store) implements `IVariableBag` and
returns it from `runner.CreateParentBag()`. That single seam is the
entire integration surface.

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

- `IVariable` — `Name`, `DataType`, `Kind` (Local/Input/Output),
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
│  • List<RuntimeVariable> variables                                     │
│      └─ id (GUID) + name + typeName +                                  │
│         [SerializeReference] VariableDefault default                   │
│  • List<VariableEdge> variableEdges                                    │
│      └─ variableId + toNodeId + toPortName                             │
└──────────────────┬─────────────────────────────────────────────────────┘
                   │ GraphBuilder.Build
                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│  RUN TIME                                                              │
│  • InMemoryVariableBag : IVariableBag                                  │
│      Dictionary<string /*id*/, VariableCell>                           │
│      Parent → runner.CreateParentBag() (consumer hook)                 │
│  • VariableCell { Id, Type, Value, event Changed }                     │
│  • Flow.Variables = bag                                                │
│  • VariableEdges  → DataBinding closures reading flow.Variables        │
│  • Get/Set/Observe Variable nodes drive flow.Variables directly        │
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

```csharp
namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class VariableDefault
    {
        public abstract object? GetBoxedValue();
        public abstract Type ValueType { get; }
    }

    [Serializable]
    public sealed class IntDefault : VariableDefault {
        public int value;
        public override object? GetBoxedValue() => value;
        public override Type ValueType => typeof(int);
    }
    [Serializable] public sealed class FloatDefault   : VariableDefault { /* float */ }
    [Serializable] public sealed class BoolDefault    : VariableDefault { /* bool */ }
    [Serializable] public sealed class StringDefault  : VariableDefault { /* string */ }
    [Serializable] public sealed class ObjectDefault  : VariableDefault {
        public UnityEngine.Object? value;
        // SerializeReference handles UnityEngine.Object refs natively.
    }
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

```csharp
namespace Scaffold.GraphFlow
{
    public interface IVariableBag
    {
        IVariableBag? Parent { get; }
        bool TryGet<T>(string id, out T value);
        void Set<T>(string id, T value);

        // Optional change-observation surface. Implementations that
        // don't support observers return false from TryGetCell.
        bool TryGetCell(string id, out VariableCell cell);
    }

    public sealed class VariableCell
    {
        public string Id   { get; }
        public Type   Type { get; }

        object? _value;
        public object? Value
        {
            get => _value;
            set { if (!Equals(_value, value)) { _value = value; Changed?.Invoke(value); } }
        }

        public event Action<object?>? Changed;

        public VariableCell(string id, Type type, object? initial = null)
        {
            Id = id; Type = type; _value = initial;
        }
    }
}
```

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
            var type = Type.GetType(v.typeName) ?? typeof(object);
            var init = v.defaultValue?.GetBoxedValue();
            _cells[v.id] = new VariableCell(v.id, type, init);
        }
    }

    public bool TryGet<T>(string id, out T value)
    {
        if (_cells.TryGetValue(id, out var cell) && cell.Value is T t)
        { value = t; return true; }
        if (Parent != null) return Parent.TryGet(id, out value);
        value = default!; return false;
    }

    public void Set<T>(string id, T value)
    {
        if (_cells.TryGetValue(id, out var cell)) { cell.Value = value; return; }
        Parent?.Set(id, value);   // bubbles writes to whoever owns it
    }

    public bool TryGetCell(string id, out VariableCell cell)
    {
        if (_cells.TryGetValue(id, out cell!)) return true;
        if (Parent != null) return Parent.TryGetCell(id, out cell);
        cell = null!; return false;
    }
}
```

Lookup cascade: local → parent → ... → null. Writes target the
*owning* bag (the one that contains the id), so a Set on a global var
inside a graph still hits the global bag.

### 5. Flow integration

```csharp
public sealed class Flow
{
    // ... existing fields ...
    public IVariableBag Variables { get; internal set; } = null!;
}
```

`GraphBuilder.Build` (or wherever flow construction lives) calls
`runner.CreateParentBag()` and assigns:

```csharp
flow.Variables = new InMemoryVariableBag(asset.variables, runner.CreateParentBag());
```

### 6. Consumer hook on `GraphRunner`

```csharp
public abstract class GraphRunner
{
    // ... existing members ...

    /// <summary>Override to inject a parent bag (shared variables,
    /// save state, network store, entities VariableBag adapter, ...).</summary>
    protected internal virtual IVariableBag? CreateParentBag() => null;
}
```

A consumer that wants to bridge `com.scaffold.entities`'s
`VariableBag` writes a tiny adapter (≈30 lines) in their own
assembly; GraphFlow runtime never references entities.

### 7. Connection refactor for variable-bound ports

`Connection<T>` (the typed binding cast at the runtime port boundary)
already wraps a `Func<Flow, T>` accessor. Adding a variable-bound
form is a new factory:

```csharp
public static Connection<T> FromVariable(string variableId, T fallback = default!)
    => new(flow =>
    {
        return flow.Variables.TryGet<T>(variableId, out var v) ? v : fallback;
    });
```

The bake step picks `FromVariable` instead of the literal/port
factory when the source of an input port is an `IVariableNode`. The
node body (`Speed.Read(flow)`) is untouched — same call site, same
performance characteristics, single dictionary lookup.

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

These are normal runtime nodes; they are *not* port-bind sugar:

```csharp
[GraphNode("Variables/Get")]
public sealed class GetVariableNode<T> : RuntimeNode
{
    [SerializeField] string variableId;
    public OutputPort<T> Value;

    protected override void OnBuild()
    {
        Value = new OutputPort<T>(flow =>
            flow.Variables.TryGet<T>(variableId, out var v) ? v : default!,
            cache: false);
    }
}

[GraphNode("Variables/Set")]
public sealed class SetVariableNode<T> : RuntimeNode<TRunner>
{
    [SerializeField] string variableId;
    public InputPort<T> NewValue;
    public FlowInPort  In;
    public FlowOutPort Done;

    protected override void OnBuild()
    {
        In = FlowInPort.Sync(this, nameof(In), flow =>
        {
            flow.Variables.Set(variableId, NewValue.Read(flow));
            return Done;
        });
    }
}
```

`Observe` (event-on-change) uses `IVariableBag.TryGetCell` and hooks
the `Changed` event in `OnFlowStart`, unhooks in `OnFlowEnd`.

The source generator (`GraphPackageTrioEmitter`) emits one Get/Set
pair per registered `VariableDefault` subclass — same
per-type-emission machinery already used for typed ports.

---

## Phasing

Build green at every commit, mirror the M3 / Runner-Redesign
discipline. Suggested ordering:

| Phase | Lands | Touches |
|---|---|---|
| 1 | `VariableDefault` hierarchy + initial 5 subclasses, `RuntimeVariable`, `VariableEdge`, schema bump on `GraphAsset` (empty lists, non-breaking) | Runtime |
| 2 | `IVariableBag`, `VariableCell`, `InMemoryVariableBag`, `Flow.Variables`, `runner.CreateParentBag()` hook | Runtime |
| 3 | Bake-time emission of `RuntimeVariable[]`; stop skipping `IVariableNode`; emit `VariableEdge[]`; `Connection.FromVariable` plumbed | Editor + Runtime |
| 4 | `GetVariableNode<T>` / `SetVariableNode<T>` + source-generator emission for the registered defaults | Generator + Runtime |
| 5 | `ObserveVariableNode<T>` + `TryGetCell` + cell-change events | Runtime |
| 6 | Sample (CardSandbox or fresh) graph using the variable system end-to-end | Sample |

Each phase ends with the snapshot harness green and Unity batchmode
compile zero `error CS` lines. Phases 1+2 are trivially independent
of GT; phase 3 is the only one touching GT internals.

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

GT's `IVariable` exposes `Name` and `DataType` publicly but not a
GUID. We have two options:

- **Reflection on the GT model implementation field** — same trick
  `EditorNodeIdentity.cs` already uses for nodes. Stable across GT
  versions so far but unsupported.
- **Hash the name** — simple, but breaks on rename.

Reflection is the better default; fall back to hash + warn if the
reflection path ever breaks under a GT update. Worth confirming the
field is reachable in 0.4-exp before phase 3.

### 3. `CreateParentBag()` lifetime

Currently proposed as `protected internal virtual` on `GraphRunner`,
called once at `GraphBuilder.Build` time. Two flavors to decide
between:

- **Per-runner** — `IVariableBag? CreateParentBag()`, called once.
  Simpler. Fine for "the runner's owner has one parent bag for the
  agent."
- **Per-flow** — `IVariableBag? CreateParentBag(Flow flow)`, called
  per `Run`. More flexible (different flows get different parents
  based on payload). Costs nothing if unused.

Per-flow is strictly more general; recommend that unless there's a
reason to forbid mid-flow parent swaps.

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

When a Set targets a variable owned by a parent bag:

- **Bubble up** (current sketch) — write goes to the bag that owns
  the id. Mirrors closure semantics in normal programming languages.
- **Shadow locally** — write creates/updates a local cell, leaving
  the parent untouched. Matches some scripting languages
  (Python `nonlocal`).

Bubble-up is the FlowCanvas behavior and the less surprising default.
Shadowing is potentially useful for "scratch override of a global
within this graph's run" — could be a per-Set-node toggle. Default
to bubble-up; defer the toggle.

### 6. Read fallback when type mismatches

`InMemoryVariableBag.TryGet<T>` checks `cell.Value is T`. If a
designer accidentally binds an `int` port to a `float` variable,
the bake should reject it. But what about polymorphism (a
`GameObject` variable read as a `Component`)? Either:

- **Strict equality** — `cell.Type == typeof(T)`. Safest, blocks
  some valid uses.
- **Assignability** — `typeof(T).IsAssignableFrom(cell.Type)`. More
  permissive, matches what users expect from C#.

Behavior Graph's docs explicitly call out implicit casting between
BBVs and node fields. Assignability + boxing is the better default;
strict-equality only at *bake-time validation*.

### 7. Subgraphs / variable scoping

`ExecPlan-v2.md` lists subgraphs as a v2 feature; this plan inherits
that. When subgraphs land, GT's `IVariable.Kind` (Local/Input/Output)
becomes meaningful — Input/Output are subgraph parameters. The
plumbing is straightforward (a sub-bag layered over the host bag),
but the design intersects with however subgraphs end up being
modeled. Out of scope here; flag for the subgraph plan.

### 8. Save / load

FlowCanvas's `BlackboardSource` is JSON-serializable for save games.
Our `InMemoryVariableBag` isn't — `VariableCell.Value` is `object?`.
Two paths when this comes up:

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
set in `TryGet`); cheap to skip in the v1 sketch. Add when adding
sample tests.

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
