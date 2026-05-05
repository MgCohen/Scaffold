# GraphFlow Runner Redesign — Plan & Sketches

Status: **Draft v2 — major rewrite after design conversation. Shape locked, ready to implement.**

This doc replaces the v1 sketch. The shape changed substantially after a
design pass that pulled the model closer to FlowCanvas / Unreal Blueprint
idioms (pull-based dataflow + push-based execution, per-In handlers,
flow-keyed caches) while keeping the framework's existing strengths
(typed ports, source-generated catalogs, no reflection at runtime).

> **Reading prerequisites.** This doc references types, attributes,
> validators, and generator behavior from the existing GraphFlow
> package without re-explaining them. Pair it with:
> - `Assets/Packages/com.scaffold.graphflow/` (the source — types
>   like `Port`, `Flow`, `Connection<T>`, built-in nodes, etc.).
> - `GraphFlow-Audit.md` at the repo root (recent architecture
>   audit — Batch references, validator IDs `EFG-V*`, the
>   `PortValueResolver` etc. all originate there).

---

## What changed from v1

The v1 draft kept `RuntimeNode.Execute(Flow)` as the single per-node
chokepoint and folded `GraphController` + `GraphExecutor` into the
runner. Most of that survived. The deeper changes:

- **Execute is gone.** Replaced by per-`FlowInPort` callback handlers
  set in node ctors. Nodes no longer override an Execute method;
  imperative behavior lives on the In ports themselves. Multi-In
  becomes natural — necessary for loops (now on the roadmap).
- **Pure nodes have no flow ports.** Add, Subtract, And, etc. are
  pure compute — lazy `OutputPort<T>` reads only. No In, no Out,
  no handler. Matches FlowCanvas's PureFunctionNode model.
- **Flow becomes the per-run identity.** Carries payload, outcome,
  cancellation, runner ref, per-flow port-value cache. Pooled to
  hold steady-state allocation at zero.
- **Ports are flow-keyed memo caches.** `OutputPort<T>.Read(Flow)`
  computes once per flow and caches. Loops invalidate via
  `flow.InvalidateAll()`.
- **Walk loop returns `FlowOutPort.End`** (a sentinel) instead of
  null. `flow.Return(value)` and `flow.Cancel()` return the sentinel
  so handlers compose into one-line expressions.
- **Builder owns construction**, including the runner. Public API is
  `builder.Build(asset)` returning a fresh runner; the builder caches
  bake results per asset. No static cache classes.
- **Bake is named explicitly** (was "Hydrate" in v1). Topology
  resolution + node wiring happen once per asset, shared across all
  runners spawned from that asset.
- **Nodes are shared across runners.** Per-run state lives on Flow,
  per-port cache is keyed by Flow, runners hold no per-node state.
  The 5-cards-in-hand pattern works with one bake + many cheap
  runners.
- **Typed runner access via `RuntimeNode<TRunner>`** static helper
  — no cached `_runner` field on nodes (would be per-runner state
  on a shared instance).

---

## Background

### Where we are

After the audit cleanup (16 audit items resolved across 8 commits +
a final comment-strip pass), the runtime is:

```
GraphAsset<TRunner>           ScriptableObject — pure data (nodes + edges).
GraphController<TRunner>      Hydrates ports, builds entry map, dispatches Run<TEntry>.
GraphExecutor (static)        4-line walk loop. Static helper.
GraphRunner                   Empty marker base — services bag (subclass adds them).
EntryRuntimeNodeBase          Abstract: PayloadType, Run(object), BindForRun<TRunner>.
EntryRuntimeNode<TEntry>      Holds Payload, has a typed Run(TEntry), wires _runFromHere closure.
RuntimeNode                   Base for all nodes. Has Ports dict + Bind() for hydration wiring.
RuntimeNode<TRunner>          Typed flow node base — gets a runner ref via BindRunner.
```

Consumer setup today (the four-line dance):

```csharp
var runner     = new CardEffectRunner(rules, bus);
var controller = new GraphController<CardEffectRunner>(asset);
controller.Initialize(runner);
await controller.Run(new OnPlay { ... });
```

### What we want

- **Custom entry shapes** — e.g., card entries with two flow outs:
  `Validate` (must terminate at `Return<bool>`) and `Execute`.
- **Loops** — multi-In nodes (Begin / Continue), with a clean
  invalidation story so iteration K+1 re-evaluates instead of
  reusing iteration K's cached values.
- **Concurrent runs on the same runner** — five cards in hand
  spawn five runners; each runner can be re-entered (recursive
  triggers) without per-call setup.
- **Trigger payload modification** — graphs that modify the
  inflight trigger payload (or cancel propagation entirely).
- **Per-node hooks** — Initialize seam for "subscribe to events,
  cache state" without intruding on wiring.

---

## Design tenets

Architectural assumptions baked into every decision below.

- **Many-to-one runners-to-asset.** A runner instance is bound to
  exactly one asset. The same asset can build many runners —
  typical pattern: one runner per in-game thing (per card, per
  encounter, per session) all built from the same `*.card` /
  `*.graph` asset.

- **Concurrent runs on the same runner are first-class.** All
  per-run state lives on `Flow`, never on the runner or on nodes.
  Nodes are stateless after bake; the runner holds only graph-level
  state. The same runner can be `Run` repeatedly in parallel
  without coordination — both for game-driven concurrency (one
  card triggering itself recursively) and for host-driven
  concurrency (parallel dispatch on the same runner instance).

- **Bake-once, build-many.** Topology resolution and node
  instantiation happen exactly once per asset. Spawning a runner
  from an already-baked asset is just constructing the runner;
  the wired node graph is shared across all runners built from
  that asset. Per-runner state is only what the consumer puts
  there (services).

- **Framework provides primitives; host writes the coordination
  layer.** The package ships `Flow.Outcome`, `Return<T>`, `Cancel`,
  `OnTrigger<TEvent>`, etc. The host (game code) composes them
  into things like a trigger bus that iterates subscribed runners,
  fires payloads, reads outcomes. There is no built-in trigger
  bus, no built-in subscription registry — those belong to the
  game.

- **Forward flow, backward data, ports as the threading.** Flow
  goes from entry to terminator via In→Out→In links. Data is
  pulled lazily backward when a node reads an input. Ports are
  the only objects that touch both — they're how the two
  directions connect. This framing is the spine of the design.

- **Asset is read at bake time only.** Once `Bake` returns, the
  runtime never touches `asset.nodes` / `asset.connections` /
  `asset.flowEdges` again. Direct port refs carry all the wiring.
  This is the existing G+2 invariant from `GraphFlow-Audit.md`
  ("hydration-once / direct-refs-thereafter") — preserved.

- **Single-threaded reads (for now).** The per-port cache is a
  plain `Dictionary<Flow, T>`. Concurrent runs from a
  `SynchronizationContext` (Unity's main thread) interleave but
  don't race. True multi-threaded reads from worker threads are
  out of scope; revisit if a job-system integration becomes a real
  ask.

---

## Driving use cases

Pseudo-code intent only. Specific names / signatures live in the
architecture section.

### UC1 — Entry with multiple typed flow outs

> A package's entry exposes more than one flow exit, each with its
> own expected result type. The consumer dispatches a payload
> through one specific exit and gets back its typed result.

```
# package author declares (once, at package level):
entry shape for this package =
    flow-out "Validate" returning bool
    flow-out "Execute"  returning nothing

# at runtime:
runner = build runner with services from asset
ok = runner.RunValidate(payload)        → bool
if not ok: stop
runner.RunExecute(payload)              → void
```

Constraints implied:
- Result type enforceable at edit time (validator: a graph wired
  to `Validate` must terminate at `Return<bool>`).
- Flow exits are package-uniform (every entry in the package has
  the same shape) — matches "per-package + per-type" preference.

### UC2 — Loops (multi-In nodes)

> A loop node has multiple flow entry points: `Begin` for the first
> iteration and `Continue` for subsequent ones. The body runs once
> per iteration; data ports inside the body re-evaluate each time.

```
loop body = subgraph after Body output
iteration counter lives on Flow, not on this
each iteration: invalidate previous iteration's port caches
```

Constraints implied:
- Multi-In nodes must be expressible. Single-Execute models can't
  do this without a discriminator parameter.
- The framework needs a way to invalidate a flow's accumulated
  port caches between iterations.
- Iteration state (counter etc.) must be per-flow, not per-node
  (concurrent loop runs share the node instance).

### UC3 — Modify a trigger payload mid-bus

> A trigger fires an event payload to subscribed graphs. A graph
> can modify the inflight payload (e.g., bump a damage amount by
> +1) or cancel propagation entirely. The host bus chains
> subscribers and reads the final payload (or null if cancelled).

```
# host fires trigger:
current = original event
for each subscribed runner (filtered by timing):
    flow = await runner.Run(current)
    if flow.IsCancelled: return null
    # `current` was mutated in place — payload threading is
    # automatic for reference-typed payloads
return current
```

Constraints implied:
- "Modify" is one mechanism. Direction picked: edit-in-place
  (reference-typed payloads, mutation visible after the run).
  Implementation mechanism (per-payload generated nodes vs.
  generic + accessor closures) deferred — see Q-modify.
- "Cancel" is distinct from "no modification" — already handled
  by `Flow.Outcome`.

### UC4 — Wrap node execution with custom logic

> Reframed from v1. With Execute gone, there's no longer a single
> chokepoint to wrap. Cross-cutting concerns (logging, profiling,
> gating) attach via `Initialize` hooks at bake time, or — if we
> ever need it — via a future walk-loop interception point. Not
> a v2 priority.

### UC5 — Init seams (per-node + per-runner)

> When a runner is built, each node gets an Initialize hook for
> per-node setup (subscribe to events, cache derived data). The
> runner gets its own Initialize hook for runner-level setup.
> Wiring is framework-private and never on the override surface.

```
# builder orchestrates:
build(asset):
    bake (one-time per asset, cached on builder):
        resolve topology, create connections, instantiate nodes,
        wire connections to ports
    construct runner via CreateRunner(baked)  ← consumer override
    foreach node: node.Initialize(runner)
    runner.Initialize()
    return runner
```

Constraints implied:
- Wiring is in `Bake`, not on either `Initialize` override
  surface. Consumer overrides can't accidentally bypass it.
- `Initialize` defaults are no-ops. Forgetting `base.Initialize()`
  doesn't break anything — consumer overrides are pure additions.

---

## Design decisions

### D1 — Pull model: flow forward, data backward

**Decision.** Flow walks forward via FlowIn → FlowOut → FlowIn
links. Data pulls backward via lazy `Read(Flow)` calls on input
ports, which read upstream output ports, which compute on demand.
Two direction grammars, one set of ports threading them.

Two node tiers fall out of this:

| Tier | Has FlowIn? | Has FlowOut? | Has OutputPort? | Behavior lives in |
|---|---|---|---|---|
| **Pure** | no | no | yes | OutputPort lambda |
| **Imperative** | yes (1+) | yes (0+) | optional | FlowInPort handler |

Pure nodes (Add, Branch's Condition input, Return's Value input):
no flow ports, lazy outputs.
Imperative nodes (Branch, Wait, Return, Cancel, Loop): per-In
handlers, optional outputs.

**Rationale.**

- Matches FlowCanvas / Unreal Blueprint conventions. Authors
  coming from those tools will recognize the model immediately.
- Node bodies stay local: the closure that computes a value sits
  next to the port that exposes it. The handler that runs on
  flow-arrival sits next to the port that receives it.
- Pure compute stays cheap (no execute scheduling), imperative
  flow is explicit.

### D2 — Per-FlowInPort handlers replace `Execute`

**Decision.** Imperative nodes don't override `Execute`. Each
`FlowInPort` is constructed with a handler that returns the next
`FlowOutPort` to walk to (or `FlowOutPort.End` to stop). Multi-In
nodes declare multiple FlowInPort fields, each with its own
handler.

```csharp
In = new FlowInPort(flow => Condition.Read(flow) ? True : False);
```

**Rationale.**

- Loops need multi-In (Begin / Continue / Break). Single-Execute
  forces a discriminator parameter or an internal state machine.
- Each entry point's logic stays local — no `if (lastIn == In_A)`
  switching in one method.
- Handler signature `Func<Flow, FlowOutPort>` is symmetric with
  `OutputPort<T>`'s `Func<Flow, T>` — both active port types take
  their behavior in the constructor.
- The framework owns the walk loop; handlers are pure functions
  of `flow → next port`. No imperative `flow.GoTo` ceremony.

### D3 — `Flow` as per-run identity + payload + cache key

**Decision.** `Flow` is the per-run context object. It carries:
- Payload (set once, read by entry data ports).
- Outcome (`Cancelled` / `Returned` / running).
- Result (for `Return<T>`).
- Cancellation token.
- Runner back-reference (typed access via `RuntimeNode<TRunner>`).
- Per-loop "slot" storage (iteration counters, etc.).
- A `_touchedPorts` list for cache cleanup.

`Flow` is pooled. Steady-state per-Run allocation is zero.

**Rationale.**

- Per-run state must not live on shared nodes (D5). Flow is the
  only object whose lifetime matches a single Run.
- Pooling addresses the obvious "Flow allocation per Run" cost.
  The pool holds N pre-allocated Flows; each tracks the ports it
  touches; on completion it returns to the pool, walking the
  touched list to clear per-port caches.
- Slots replace per-node mutable state for things like loop
  iteration counters. A Loop node looks up `flow.GetSlot<int>(this)`
  — keyed by `this` (the node) so concurrent runs of the same Loop
  don't collide.

### D4 — Ports own their Flow-keyed cache

**Decision.** `OutputPort<T>` holds a `Dictionary<Flow, T>` cache.
First `Read(flow)` computes via the lambda and stores; subsequent
reads in the same flow return the cached value. On flow completion,
the framework walks the flow's touched-ports list and clears
entries for that flow.

Per-port `cache: false` opt-out for ports that should always
re-evaluate (`Time.time` reads, RNG samples, loop iteration
counters).

`flow.InvalidateAll()` clears all touched-port caches for the
current flow — used by Loop between iterations.

**Rationale.**

- Memoization within a flow matters when an output is read by
  multiple consumers (Add wired to three downstream nodes).
- Cache lifetime is bounded: Flow lifecycle = cache lifecycle.
- Per-port dict is small (avg N entries, N = pool size).
- Loop invalidation is targeted (O(touched ports), not O(graph)).
- Single-threaded read contract sidesteps concurrent-write races
  on the dict for now.

### D5 — Nodes are shared across runners

**Decision.** Bake instantiates one set of `RuntimeNode` instances
per asset. All runners built from that asset reference the same
node instances. Nodes have no per-runner state — no `_runner`
field, no per-runner caches. The runner reference reaches a node
only via `flow.Runner` at execute time.

**Rationale.**

- Bake once, build many. The 5-cards-in-hand case spawns 5
  runners but pays the bake cost (topology resolution + node
  instantiation + connection wiring) exactly once.
- Per-run state is on Flow (D3); per-port cache is keyed by Flow
  (D4). With these two, there's nothing left for nodes to hold
  per-runner.
- The `_runner` field on `RuntimeNode<TRunner>` from v1 goes away
  — it would have been per-runner state on a shared instance,
  contradicting this tenet.

### D6 — Builder owns construction, hydration cached on builder

**Decision.** A typed `GraphBuilder<TRunner>` is the consumer's
entry point for spawning runners. The builder:
- Holds a `Dictionary<GraphAsset, BakedGraph>` cache (per builder
  instance, not static).
- Calls `Bake(asset)` on cache miss to do the O(N+E) topology
  pass.
- Constructs the runner by calling a consumer-implemented
  abstract `CreateRunner(BakedGraph)`.
- Calls `node.Initialize(runner)` for each node, then
  `runner.Initialize()`.

Consumer subclasses the builder once per package, holds services
as fields, supplies the runner via `CreateRunner`.

```csharp
var builder = new CardEffectBuilder(rules, bus);
var runners = Enumerable.Range(0, 5)
    .Select(_ => builder.Build(asset))
    .ToArray();
```

**Rationale.**

- The builder, not the runner, is the natural owner of the bake
  cache. Same lifecycle, same scope.
- Consumer's runner subclass keeps a single ctor that takes
  `(BakedGraph baked, ...services)` — no two-phase setup, no
  Attach method.
- DI fits later: register the builder with the container, get it
  injected, call `Build`.
- No `_built` guard on the runner — runners are constructed once
  per call by definition.

### D7 — Typed runner access via `RuntimeNode<TRunner>` static helper

**Decision.** `RuntimeNode<TRunner>` (the typed base) provides a
`protected static TRunner Runner(Flow flow) => (TRunner)flow.Runner;`
helper. Consumer nodes that need typed access to package services
derive from it; framework nodes (Add, Branch, Return) stay on the
untyped `RuntimeNode`.

**Rationale.**

- Cast is unavoidable (nodes shared, runner per-flow). The static
  helper is the smallest sugar that makes the cast disappear at
  call sites.
- Framework builtins remain runner-agnostic.
- No generic Flow<TRunner>; that approach forces TRunner into
  every port type or just relocates the cast to the handler entry.

### D8 — Whole-payload port on `EntryRuntimeNode<TPayload>` base

**Decision.** The typed entry base exposes `OutputPort<TPayload>
Payload` for free. Consumer subclasses inherit it; the generator
emits zero extra lines.

```csharp
public abstract class EntryRuntimeNode<TPayload> : EntryRuntimeNodeBase
    where TPayload : class
{
    public override Type PayloadType => typeof(TPayload);
    public OutputPort<TPayload> Payload { get; }

    protected EntryRuntimeNode()
    {
        Payload = new OutputPort<TPayload>(flow => flow.GetPayload<TPayload>()!);
        Ports.Add(nameof(Payload), Payload);
    }
}
```

**Rationale.**

- Currently impossible to wire the whole payload to a downstream
  node (only individual fields are exposed).
- Captured-nothing closure → static cached delegate, zero
  per-instance allocation.
- Foundation for the deferred Modify/Decompose/Construct work
  (those nodes will wire their `Source` input to this port).

---

## Architecture sketch

### File layout

```
Runtime/
  Asset/      GraphAsset.cs                  (unchanged)
  Builder/
    GraphBuilder.cs                          NEW — abstract<TRunner>, owns bake cache
    BakedGraph.cs                            NEW — wired-graph DTO
    GraphTopology.cs                         NEW — internal Bake() pass
    GraphRunner.cs                           (executor walk + dispatch + entry lookup)
    GraphController.cs                       DELETED
    GraphExecutor.cs                         DELETED
  Flow/       Flow.cs                        (payload + slots + InvalidateAll + pool)
  Markers/    PortMeta, CatalogEntry, …      (unchanged for now)
  Nodes/
    RuntimeNode.cs                           (Initialize hook, no Execute)
    RuntimeNode_TRunner.cs                   (typed base + Runner(Flow) helper)
    EntryRuntimeNode.cs                      (typed base + Payload port)
    Builtin/  …                              (rewritten — see below)
  Ports/
    Port.cs                                  (base + IsConnected on InputPort<T>)
    OutputPort.cs                            (cache, opt-out flag)
    FlowInPort.cs                            (handler-in-ctor, sync/async overloads)
    FlowOutPort.cs                           (.End sentinel)
    Connection.cs                            (unchanged)
```

### Port primitives

```csharp
public abstract class Port
{
    public RuntimeNode Owner { get; internal set; } = null!;
    public string      Name  { get; internal set; } = "";
}

public class OutputPort<T> : Port
{
    readonly Func<Flow, T> _compute;
    readonly Dictionary<Flow, T> _cache = new();
    readonly bool _shouldCache;

    public OutputPort(Func<Flow, T> compute, bool cache = true)
    {
        _compute = compute;
        _shouldCache = cache;
    }

    public T Read(Flow flow)
    {
        if (!_shouldCache) return _compute(flow);
        if (_cache.TryGetValue(flow, out var v)) return v;
        v = _compute(flow);
        _cache[flow] = v;
        flow.RegisterTouched(this);
        return v;
    }

    internal void ClearCache(Flow flow) => _cache.Remove(flow);
}

public class InputPort<T> : Port
{
    OutputPort<T>? _source;
    public bool IsConnected => _source != null;
    public T Read(Flow flow) => _source is null ? default! : _source.Read(flow);
    internal void Connect(OutputPort<T> source) => _source = source;
}

public class FlowOutPort : Port
{
    public static readonly FlowOutPort End = new() { Name = "<end>" };
    public FlowConnection? Connection { get; internal set; }
}

public delegate FlowOutPort           FlowHandler     (Flow flow);
public delegate ValueTask<FlowOutPort> AsyncFlowHandler(Flow flow);

public class FlowInPort : Port
{
    public FlowConnection? Connection { get; internal set; }
    internal Func<Flow, ValueTask<FlowOutPort>> Invoke { get; }

    public FlowInPort(FlowHandler      sync ) { Invoke = f => new(sync(f));     }
    public FlowInPort(AsyncFlowHandler async_) { Invoke = async_; }
}
```

### `Flow`

```csharp
public sealed class Flow
{
    object? _payload;
    Dictionary<object, object>? _slots;        // per-(node, key) per-flow state
    readonly List<Port> _touched = new();

    public GraphRunner    Runner { get; internal set; } = null!;
    public CancellationToken Token { get; internal set; }

    public Outcome Outcome { get; private set; }
    public bool    IsCancelled  => Outcome == Outcome.Cancelled;
    public bool    IsTerminating => Outcome != Outcome.Running;

    // Payload
    public T?   GetPayload<T>() where T : class => _payload as T;
    internal void SetPayload(object p) => _payload = p;

    // Outcome-setting helpers — return End so handlers compose
    public FlowOutPort Return<T>(T value) { _result = value; Outcome = Outcome.Returned;  return FlowOutPort.End; }
    public FlowOutPort Cancel()           {                  Outcome = Outcome.Cancelled; return FlowOutPort.End; }
    object? _result;
    public T? ReadResult<T>() => _result is T t ? t : default;

    // Cache touch tracking
    internal void RegisterTouched(Port p) => _touched.Add(p);
    public   void InvalidateAll()
    {
        foreach (var p in _touched) (p as ICacheClearable)?.ClearCache(this);
        _touched.Clear();
    }

    // Slots — per-node per-flow scoped state (loop counters etc.)
    public T GetSlot<T>(object owner) =>
        _slots != null && _slots.TryGetValue(owner, out var v) ? (T)v : default!;
    public void SetSlot<T>(object owner, T value) =>
        (_slots ??= new())[owner] = value;

    internal void ResetForReuse()
    {
        _payload = null; _result = null; Outcome = Outcome.Running;
        _slots?.Clear(); _touched.Clear();
    }
}
```

### `RuntimeNode` and `RuntimeNode<TRunner>`

```csharp
public abstract class RuntimeNode
{
    public int    nodeId;
    public string editorGuid = string.Empty;
    [NonSerialized] public readonly Dictionary<string, Port> Ports = new();

    // Framework wiring step — called by Bake. Default attaches the
    // pre-resolved connections to this node's ports.
    internal virtual void Build(in NodeBuildSlice slice) { /* ... */ }

    // Consumer extension point — called by builder after every node
    // is wired. Default no-op. Override to subscribe to events,
    // cache derived state using runner services.
    public virtual void Initialize(GraphRunner runner) { }
}

public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
{
    // Typed access to runner from inside FlowInPort handlers /
    // OutputPort lambdas — `Runner(flow).Rules.Foo()`.
    protected static TRunner Runner(Flow flow) => (TRunner)flow.Runner;

    // Typed Initialize sugar.
    public sealed override void Initialize(GraphRunner runner) => Initialize((TRunner)runner);
    public virtual void Initialize(TRunner runner) { }
}
```

Three-step node lifecycle:
- **ctor** (parameterless): runs at deserialization or instantiation
  via the catalog factory. Populates the `Ports` dict and wires
  per-In handlers via FlowInPort ctor.
- **`Build`** (framework-internal): called by `Bake` once per node.
  Attaches the pre-resolved slice to the node's ports.
- **`Initialize`** (consumer hook): called by the builder after all
  nodes are wired. Default no-op.

### `EntryRuntimeNode<TPayload>`

```csharp
public abstract class EntryRuntimeNodeBase : RuntimeNode
{
    public abstract Type PayloadType { get; }
}

public abstract class EntryRuntimeNode<TPayload> : EntryRuntimeNodeBase
    where TPayload : class
{
    public override Type PayloadType => typeof(TPayload);
    public OutputPort<TPayload> Payload { get; }

    protected EntryRuntimeNode()
    {
        Payload = new OutputPort<TPayload>(flow => flow.GetPayload<TPayload>()!);
        Ports.Add(nameof(Payload), Payload);
    }
}
```

No `_runFromHere`, no `BindRunner`, no `Run(TEntry)`, no `Run(object)`,
no `BindForRun<TRunner>`, no `Payload` field, no `SetPayload`. Only:
- `PayloadType` for runner-side dispatch.
- `Payload` output port for graphs that want the whole payload.
- Per-package subclasses add their own FlowOuts (Validate / Execute).

### `BakedGraph`

```csharp
public sealed class BakedGraph
{
    public IReadOnlyList<RuntimeNode>                   Nodes            { get; }
    public IReadOnlyDictionary<Type, EntryRuntimeNodeBase> EntriesByPayload { get; }

    internal BakedGraph(
        IReadOnlyList<RuntimeNode> nodes,
        IReadOnlyDictionary<Type, EntryRuntimeNodeBase> entries)
    {
        Nodes = nodes;
        EntriesByPayload = entries;
    }
}
```

### `GraphBuilder<TRunner>`

```csharp
public abstract class GraphBuilder<TRunner> where TRunner : GraphRunner
{
    readonly Dictionary<GraphAsset, BakedGraph> _cache = new();

    public TRunner Build(GraphAsset<TRunner> asset)
    {
        if (!_cache.TryGetValue(asset, out var baked))
            _cache[asset] = baked = GraphTopology.Bake(asset);

        var runner = CreateRunner(baked);
        foreach (var n in baked.Nodes) n.Initialize(runner);
        runner.Initialize();
        return runner;
    }

    protected abstract TRunner CreateRunner(BakedGraph baked);
}

internal static class GraphTopology
{
    public static BakedGraph Bake(GraphAsset asset)
    {
        // O(N+E) topology pass — same shape as v1 but with the
        // per-In handler model. See GraphFlow-Audit G+2 invariant.
        // 1. Build node-id → node map (asset.nodes are already
        //    instantiated RuntimeNodes from deserialization).
        // 2. Bucket asset.connections (data) and asset.flowEdges
        //    (flow) by destination node-id.
        // 3. Create FlowConnection instances exactly once each.
        // 4. For each node: hand it its slice via node.Build(slice).
        // 5. Build entriesByPayload dict.
        // 6. Return BakedGraph(nodes, entriesByPayload).
    }
}
```

### `GraphRunner`

```csharp
public abstract class GraphRunner
{
    protected internal IReadOnlyList<RuntimeNode>                   Nodes            { get; }
    protected internal IReadOnlyDictionary<Type, EntryRuntimeNodeBase> EntriesByPayload { get; }

    protected GraphRunner(BakedGraph baked)
    {
        Nodes            = baked.Nodes;
        EntriesByPayload = baked.EntriesByPayload;
    }

    public virtual void Initialize() { }   // consumer hook, default no-op

    // Default-flow-out dispatch by payload type.
    public Task<Flow> Run<TEntry>(TEntry payload, CancellationToken ct = default)
        where TEntry : class
    {
        if (!EntriesByPayload.TryGetValue(typeof(TEntry), out var entry))
            throw new InvalidOperationException($"No entry for {typeof(TEntry).FullName}.");

        // Entry's default FlowOut walks via its sole connection.
        // For per-package shapes with multiple FlowOuts, see
        // RunFromFlowOut below.
        var flow = AcquireFlow(payload, ct);
        return RunFromEntry(entry, flow);
    }

    // Explicit-flow-out dispatch (multi-out entry shapes).
    protected async Task<TResult?> RunFromFlowOut<TResult>(
        object payload, FlowOutPort flowOut, CancellationToken ct = default)
    {
        var dest = flowOut.Connection?.Destination;
        if (dest == null) return default;

        var flow = AcquireFlow(payload, ct);
        await RunFromInPort(dest, flow);
        return flow.ReadResult<TResult>();
    }

    Flow AcquireFlow(object payload, CancellationToken ct)
    {
        var flow = FlowPool.Rent();
        flow.SetPayload(payload);
        flow.Token  = ct;
        flow.Runner = this;
        return flow;
    }

    async Task<Flow> RunFromEntry(EntryRuntimeNodeBase entry, Flow flow)
    {
        // Walk starts at the destination of the entry's default Out.
        // (For multi-Out entries, RunFromFlowOut is the typed path.)
        var defaultOut = entry.GetDefaultOut();
        var dest = defaultOut?.Connection?.Destination;
        if (dest != null) await RunFromInPort(dest, flow);
        return flow;
    }

    async Task RunFromInPort(FlowInPort start, Flow flow)
    {
        FlowInPort? current = start;
        while (current != null)
        {
            var next = await current.Invoke(flow);
            if (ReferenceEquals(next, FlowOutPort.End)) break;
            current = next.Connection?.Destination;
        }
        FlowPool.Return(flow);   // clears caches via flow.InvalidateAll under the hood
    }
}
```

### Per-package shape

Hand-written by the consumer (one builder + one runner subclass per
package, plus a per-package entry base class):

```csharp
// Per-package entry base — declares the multi-out shape.
public abstract class CardEntry<TPayload> : EntryRuntimeNode<TPayload>
    where TPayload : class
{
    public FlowOutPort Validate { get; }
    public FlowOutPort Execute  { get; }

    protected CardEntry()
    {
        Validate = new FlowOutPort { Owner = this, Name = nameof(Validate) };
        Execute  = new FlowOutPort { Owner = this, Name = nameof(Execute)  };
        Ports.Add(Validate.Name, Validate);
        Ports.Add(Execute.Name,  Execute);
    }
}

// Generator-emitted per-payload subclass.
[Serializable]
public sealed class OnPlayRuntime : CardEntry<OnPlay>
{
    // Per-field outputs (lazy lambdas — capture nothing, static
    // cached delegates).
    public OutputPort<Card>     Card;
    public OutputPort<PlayerId> Player;

    public OnPlayRuntime()
    {
        Card   = new OutputPort<Card>    (flow => flow.GetPayload<OnPlay>()!.Card);
        Player = new OutputPort<PlayerId>(flow => flow.GetPayload<OnPlay>()!.Player);
        Ports.Add(nameof(Card),   Card);
        Ports.Add(nameof(Player), Player);
    }
}

// Per-package builder — holds services.
public sealed class CardEffectBuilder : GraphBuilder<CardEffectRunner>
{
    readonly Rules _rules;
    readonly Bus   _bus;

    public CardEffectBuilder(Rules rules, Bus bus)
    {
        _rules = rules;
        _bus   = bus;
    }

    protected override CardEffectRunner CreateRunner(BakedGraph baked)
        => new(baked, _rules, _bus);
}

// Per-package runner — services + typed dispatch sugar.
public sealed class CardEffectRunner : GraphRunner
{
    readonly Rules _rules;
    readonly Bus   _bus;

    public CardEffectRunner(BakedGraph baked, Rules rules, Bus bus) : base(baked)
    {
        _rules = rules;
        _bus   = bus;
    }

    public Task<bool> RunValidate<T>(T payload) where T : class
    {
        var entry = (CardEntry<T>)EntriesByPayload[typeof(T)];
        return RunFromFlowOut<bool>(payload, entry.Validate);
    }

    public Task RunExecute<T>(T payload) where T : class
    {
        var entry = (CardEntry<T>)EntriesByPayload[typeof(T)];
        return RunFromFlowOut<object>(payload, entry.Execute);
    }
}
```

Host call site:

```csharp
var builder = new CardEffectBuilder(rules, bus);
var runners = Enumerable.Range(0, 5).Select(_ => builder.Build(asset)).ToArray();

if (!await runners[0].RunValidate(new OnPlay { ... })) return;
await runners[0].RunExecute(new OnPlay { ... });
```

---

## Built-in nodes — final shape

### `Add` — pure, lazy, no flow ports

```csharp
[Serializable]
[GraphNode(Category = "Math")]
public sealed partial class Add : RuntimeNode
{
    public InputPort<int>  A      = null!;
    public InputPort<int>  B      = null!;
    public OutputPort<int> Result = null!;

    partial void InitializePorts() =>
        Result = new OutputPort<int>(flow => A.Read(flow) + B.Read(flow));
}
```

### `Branch` — single In, two Outs

```csharp
[Serializable]
[GraphNode(Category = "Flow")]
public sealed partial class Branch : RuntimeNode
{
    public InputPort<bool> Condition = null!;
    public FlowInPort      In        = null!;
    public FlowOutPort     True      = null!;
    public FlowOutPort     False     = null!;

    partial void InitializePorts() =>
        In = new FlowInPort(flow => Condition.Read(flow) ? True : False);
}
```

### `Return<TResult>` — terminator

```csharp
[Serializable]
[GraphNode(Category = "Flow")]
public sealed partial class Return<TResult> : RuntimeNode
{
    public FlowInPort         In    = null!;
    public InputPort<TResult> Value = null!;

    partial void InitializePorts() =>
        In = new FlowInPort(flow => flow.Return(Value.Read(flow)));
}
```

### `Cancel`

```csharp
[Serializable]
[GraphNode(Category = "Flow")]
public sealed partial class Cancel : RuntimeNode
{
    public FlowInPort In = null!;

    partial void InitializePorts() =>
        In = new FlowInPort(flow => flow.Cancel());
}
```

### `Wait` — async handler

```csharp
[Serializable]
[GraphNode(Category = "Time")]
public sealed partial class Wait : RuntimeNode
{
    public InputPort<float> Seconds = null!;
    public FlowInPort       In      = null!;
    public FlowOutPort      Out     = null!;

    partial void InitializePorts() =>
        In = new FlowInPort(async flow =>
        {
            await Task.Delay(TimeSpan.FromSeconds(Seconds.Read(flow)), flow.Token);
            return Out;
        });
}
```

### `Loop` — multi-In, the case that drove this

```csharp
[Serializable]
[GraphNode(Category = "Flow")]
public sealed partial class Loop : RuntimeNode
{
    public InputPort<int>  Count     = null!;
    public FlowInPort      Begin     = null!;
    public FlowInPort      Continue  = null!;
    public FlowOutPort     Body      = null!;
    public FlowOutPort     Done      = null!;
    public OutputPort<int> Iteration = null!;

    partial void InitializePorts()
    {
        // Iteration counter lives on Flow, not on `this` —
        // concurrent loop runs each have their own slot.
        Iteration = new OutputPort<int>(flow => flow.GetSlot<int>(this), cache: false);

        Begin = new FlowInPort(flow =>
        {
            flow.SetSlot(this, 0);
            return Count.Read(flow) > 0 ? Body : Done;
        });

        Continue = new FlowInPort(flow =>
        {
            flow.InvalidateAll();                       // wipe iteration K's caches
            var i = flow.GetSlot<int>(this) + 1;
            flow.SetSlot(this, i);
            return i < Count.Read(flow) ? Body : Done;
        });
    }
}
```

---

## Open questions

### Q-modify — Trigger payload modification mechanism

Direction picked (D-trigger / UC3): **edit-in-place** on
reference-typed payloads. Two implementation paths under
consideration; deferred to a follow-up pass:

1. **Per-payload generated nodes** (`OnPlayWrite`, etc.) — typed
   InputPorts per field, hard-coded write-back logic. More
   generated code, fully typed at runtime.
2. **Generic + accessor closures** (`Modify<TPayload>`) — one
   framework class, per-payload accessor data emitted to the
   catalog. Less generated code, slight runtime indirection.

Choice depends on how much we want to lean on the catalog versus
how much per-payload code we tolerate. Resolve when implementing.

Related: `InputPort<T>.IsConnected` is in the port API for the
"skip unwired fields" semantics regardless of which path lands.

### Q-pool — Flow pool ownership

The pool needs to live somewhere. Options:
- **Static singleton** — simplest, shared across all runners.
  Concurrency on the pool itself becomes a concern (same
  single-threaded contract or a lock).
- **Per-runner** — no contention but loses sharing across runners
  (5-card case allocates 5 pools).
- **Per-builder** — bake-cache scope, shared across that builder's
  runners. Probably the right scope.

Lean: per-builder. Settle when implementing.

### Q-walk-interception — UC2 reframe

v1's UC2 (wrap every node execution) is gone with `Execute`.
Replacement options:
- A virtual `OnFlowStep` on `GraphRunner` that wraps each handler
  invocation in the walk loop.
- Per-node opt-in: nodes wrap their own handlers in ctor with
  whatever decoration they want.
- Defer entirely until a real use case surfaces (logging,
  profiling, replay harness).

Lean: defer. The walk loop has a natural injection point if we
need it; adding it speculatively is YAGNI.

### Q-async-overload — `FlowInPort` ctor resolution

Two ctor overloads (`FlowHandler` vs `AsyncFlowHandler`) rely on
the lambda being typed as `async` for the async overload to bind.
Verify with a small test that overload resolution picks the right
one in practice. If ambiguous, fall back to two named static
factories (`FlowInPort.Sync(...)`, `FlowInPort.Async(...)`).

### Q-default-out — Entry's default FlowOut for `Run<TEntry>`

`Run<TEntry>` walks from "the entry's default FlowOut." For
single-out entries this is unambiguous. For multi-out entries
(CardEntry: Validate + Execute), what's the default?
- **No default** — `Run<TEntry>` only works for single-out
  entries; multi-out requires `RunFromFlowOut`. Cleanest.
- **Convention** — first declared FlowOut. Implicit but fragile.
- **Explicit** — `[DefaultFlowOut]` attribute on one port.

Lean: no default. `Run<TEntry>` is for the simple single-out case;
multi-out shapes use the per-package runner sugar
(`RunValidate` / `RunExecute`).

---

## Migration / cutover

Single hard-cutover commit ("refactor(GraphFlow): per-In handlers,
Bake builder, Flow as per-run context"). Old `GraphController` /
`GraphExecutor` deleted in the same commit that introduces the
new shape. No alive-but-obsolete transition window.

Order of work within the commit:

1. **Port API additions.**
   - `OutputPort<T>` — flow-keyed cache + opt-out flag.
   - `InputPort<T>.IsConnected`.
   - `FlowOutPort.End` sentinel.
   - `FlowInPort` with sync + async ctor overloads.

2. **Flow gains pool-friendly state.**
   - Payload, slots, touched-ports list, `Return`/`Cancel` return
     `FlowOutPort.End`, `InvalidateAll`, `ResetForReuse`.

3. **Add `BakedGraph` + `GraphTopology.Bake`.** Pure topology
   resolution, returns BakedGraph.

4. **Add `GraphBuilder<TRunner>`.** Cache + Build template method
   + abstract CreateRunner.

5. **Rewrite `GraphRunner`.**
   - Forced base ctor `(BakedGraph)`.
   - `Run<TEntry>` (default-out dispatch).
   - `RunFromFlowOut<TResult>` (explicit-out dispatch).
   - Walk loop (`Invoke` per FlowInPort, `End` sentinel check,
     pool return).
   - `Initialize` virtual no-op default.

6. **Rewrite `RuntimeNode`.**
   - `Build(in NodeBuildSlice)` internal virtual (framework
     wiring step).
   - `Initialize(GraphRunner)` public virtual no-op default.
   - Drop `Execute`.

7. **Rewrite `RuntimeNode<TRunner>`.**
   - Static `Runner(Flow)` helper.
   - Sealed Initialize override forwarding to typed.
   - Drop `_runner` field, drop typed Execute.

8. **Strip `EntryRuntimeNode<T>`** to: `PayloadType` + `Payload`
   port. Drop everything else.

9. **Rewrite all built-in nodes** (Add, And, Or, Not, Branch,
   GreaterThan, LessThan, Subtract, Multiply, IntToString, Return,
   Cancel) to the new shape.

10. **Generator changes.**
    - Per-payload entry runtime closure capture: `flow =>
      flow.GetPayload<T>()!.FieldX` instead of `() => Payload!.FieldX`.
    - Entry runtime no longer holds Payload field.
    - `EntryBase` property on `[GraphPackage]` if not already there.
    - Catalog emit unchanged for now (Modify/Decompose/Construct
      deferred).

11. **Delete** `GraphController.cs`, `GraphExecutor.cs`. Migrate
    all sample + test call sites to `builder.Build(asset)` /
    `runner.Run(payload)`.

12. **Run** snapshot tests + smoke tests. Refresh snapshots if
    generator output shifts.

Estimated net delete: ~250–350 lines of framework (controller +
executor + EntryRuntimeNode complexity + per-node Execute
boilerplate). Add: ~250 lines of framework (BakedGraph +
GraphBuilder + GraphTopology + per-In handler infrastructure).
Roughly net-zero on framework size; significantly different shape.
Plus generator changes for Payload-from-Flow and EntryBase emit.

---

## Test impact

- **Snapshot tests** — `HarnessGraphRegistry.g.cs` registry shape
  may change if `BindForRun` plumbing is removed; entry runtime
  closure capture changes. Snapshots regenerated post-change.
- **`Tests/RuntimeSmokeTests`** — fixtures use `controller.Initialize`
  + `controller.Run`. Migrate to `new SmokeBuilder(...).Build(asset)`
  + `runner.Run`. Trivial.
- **`CardSandbox/Tests/Strike500Tests`** — uses `trig.Run(payload)`
  for trigger pattern-match. Migrate to per-runner subscription
  pattern with `runner.Run(payload)`.
- **Editor / generator** — `RegisterAdditional` partial on
  `<Stem>GraphRegistry` is unchanged. Per-package OnTrigger /
  Return shims are unchanged (still use the existing catalog).
- **`PortValueResolver`** — unchanged (bake-time, doesn't touch
  controller/executor).
- **NEW: Loop node test** — concurrent runs, cache invalidation,
  iteration counter on flow slot. Validates the multi-In shape
  end-to-end.

---

## What this enables (besides UC1-UC5)

- **Loops with shared body subgraph state per iteration.** Per-flow
  caches + `InvalidateAll` give correct re-evaluation without
  per-loop sub-flow trees.
- **Replay / determinism harnesses** — Flow is the snapshot unit.
  Record one Flow, replay it.
- **Per-flow scoped state** via `flow.GetSlot/SetSlot` — anywhere
  a node needs per-run state without per-runner state on the node.
- **Cross-graph state sharing** — multiple runners can ref one
  services instance via DI (or just shared field on the builder).
- **Profiler integration** later via the deferred walk-step
  interception — wrap each `await current.Invoke(flow)` in a
  `BeginSample`/`EndSample`.

---

## Future considerations (out of scope for this redesign)

- **Modify / Decompose / Construct nodes for payloads.** Direction
  set (edit-in-place); mechanism deferred. Two paths sketched in
  Q-modify; pick when implementing. Will reuse `EntryRuntimeNode<T>.Payload`
  port as the wire target.
- **Walk-loop interception** for cross-cutting concerns (logging,
  profiling, gating). Deferred until concrete need.
- **Concurrent reads from worker threads.** Current contract is
  single-threaded reads (Unity's main-thread `SynchronizationContext`).
  If a job-system integration ever materializes, the per-port
  `Dictionary<Flow, T>` would need either lock-protection or
  `ConcurrentDictionary`.
- **Sub-flows / Flow trees** — considered for loop semantics
  (per-iteration child flow). Rejected in favor of `InvalidateAll`
  on a single Flow because trees complicate Return/Cancel
  propagation. Revisit only if a use case demands isolation
  guarantees we can't otherwise provide.
- **No-cache opt-out via attribute** instead of ctor flag —
  ergonomic sugar, not a behavior change.

---

## Anti-goals (what this redesign explicitly does **not** do)

- **No runtime reflection added.** All field access in entry
  runtimes is generator-emitted (lambdas closing over `flow.GetPayload<T>()`).
  Type matching at setup is compile-time via generic inference and
  the builder's `Build<T>(asset)` extension.
- **No new attributes on user payloads** — entry shapes come from
  the package's hand-written entry base, not from a forest of
  attributes per payload.
- **No DI container required** — builder ctor is whatever the user
  wants; the framework doesn't impose service injection patterns.
- **No async-over-sync** — handlers are genuinely async; no busy
  loops or sync wrappers.
- **No `Execute` virtual on `RuntimeNode`.** Imperative behavior
  is per-FlowInPort. There is no chokepoint to override.
- **No mutable per-runner state on nodes.** Nodes are shared
  across runners by construction; per-run state is on Flow.
