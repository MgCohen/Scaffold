# GraphFlow Runner Redesign — Plan & Sketches

Status: **Draft, iterating.** The shape feels right; specifics need nitpicking.

This doc captures the proposed redesign of the GraphFlow runtime API
surface — folding `GraphController<TRunner>` and `GraphExecutor` into
the `GraphRunner` itself, simplifying entry dispatch, and introducing
the right virtuals so consumers can extend behavior without resorting
to delegates or attribute soup.

The redesign came out of a design discussion that started with "how do
I make custom flow ports on entries" and snowballed into "the whole
controller/executor split is over-engineered for what consumers
actually need."

> **Reading prerequisites.** This doc references types, attributes,
> validators, and generator behavior from the existing GraphFlow
> package without re-explaining them. Pair it with:
> - `Assets/Packages/com.scaffold.graphflow/` (the source — types
>   like `Port`, `Flow`, `Connection<T>`, built-in nodes, etc.).
> - `GraphFlow-Audit.md` at the repo root (recent architecture
>   audit — Batch references, validator IDs `EFG-V*`, the
>   `PortValueResolver` etc. all originate there).

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

Three objects, two of which the consumer doesn't conceptually want to
hold (`controller` is a black box; `runner` carries services). The
trigger-bus pattern is even noisier — host must track every (runner,
asset) pair and re-do the dance.

### What we want

- **Custom entry shapes** — e.g., card entries with two flow outs:
  `Validate` (must terminate at `Return<bool>`) and `Execute`.
- **Per-node before/after wrapping** — log/time/gate every node.
  Can be expressed as a virtual override, not a delegate chain.
- **Trigger payload modification** — a graph mid-bus can change the
  inflight payload (or cancel the chain).
- **Two overridable init seams** — `runner.Initialize` (loops nodes,
  registers entries) and `node.Initialize` (per-node consumer hook).
  Wiring lives in a separate static `GraphBuilder` — not part of
  either override surface.

### Design tenets

Architectural assumptions baked into every decision below. Stating
them up-front so a reader can sanity-check the rest.

- **Many-to-one runners-to-asset.** A runner instance is bound to
  exactly one asset (you don't re-bind a runner to a different
  asset over its lifetime). The same asset can be used to build
  many runner instances — typical pattern: one runner per
  in-game thing (per card, per encounter, per session) all built
  from the same `*.card` / `*.graph` asset.

- **Concurrent runs on the same runner are expected to work.**
  A consumer should be able to call `runner.Run(...)` again before
  the prior run completes (re-entrant trigger fires, parallel
  invocations from the host) without the second call clobbering
  the first. Per-run state lives on `Flow`, not on the runner or
  on entry nodes. ⚠ This conflicts with Q7's current lean (option 3,
  "one runner per concurrent path"); Q7 needs revisiting in light
  of this tenet.

- **Framework provides primitives; host writes the coordination
  layer.** The package ships `Flow.Outcome`, `Return<T>`, `Cancel`,
  `OnTrigger<TEvent>`, etc. The host (game code) composes them
  into things like a trigger bus that iterates subscribed runners,
  fires payloads, reads outcomes. There is no built-in trigger
  bus, no built-in subscription registry — those belong to the
  game.

- **Asset is read at `Build` time only.** Once `GraphBuilder.Build`
  returns, the runtime never touches `asset.nodes` /
  `asset.connections` / `asset.flowEdges` again. Direct port refs
  carry all the wiring; the executor walks via `FlowOutPort.Connection`.
  This is the existing G+2 invariant from `GraphFlow-Audit.md`
  ("hydration-once / direct-refs-thereafter") — the redesign
  preserves it.

---

## Driving use cases

Pseudo-code intent only. The shape is what matters; specific names /
signatures / accessors are deferred to the design decisions section
so the API isn't accidentally pinned by an early sketch.

### UC1 — Entry with multiple typed flow outs

> A package's entry point exposes more than one flow exit, each with
> its own expected result type. The consumer dispatches a payload
> through one specific exit and gets back its typed result. Different
> exits can be invoked independently (or sequentially) for the same
> payload.

```
# package author declares (once, at package level):
entry shape for this package =
    flow-out "Validate" returning bool
    flow-out "Execute"  returning nothing

# at runtime:
runner = create runner with services
attach asset to runner

ok = run-through(runner, "Validate", payload)       → bool
if not ok: stop
run-through(runner, "Execute", payload)              → void
```

Constraints implied:
- The "expected result type" is enforceable at edit time
  (validator: a graph wired to `Validate` must terminate at a
  `Return<bool>`).
- Flow exits are package-uniform (every entry in the package has
  the same shape) — this matches the "per-package + per-type"
  preference.

### UC2 — wrap every node execution with custom logic

> The consumer wants to run code immediately before and immediately
> after each node executes — for logging, profiling, telemetry,
> gating, etc. Single hook per concern; the framework's default is
> "no wrapping."

```
# consumer expresses, at runner level:
override "execute one node" =
    do something before
    invoke default execute-one-node behavior
    do something after
```

Constraints implied:
- The hook is per-runner (subclass override), not a registered
  delegate chain. One override at a time.
- Default path has zero overhead when not overridden.
- Overriding "execute one node" must NOT require also overriding
  the walk loop itself.

### UC3 — modify a trigger payload mid-bus

> A trigger fires an event payload to subscribed graphs. A graph
> can modify the inflight payload (e.g., bump a damage amount by
> +1) or cancel propagation entirely. The host bus chains
> subscribers and reads the final payload (or null if cancelled).

```
# host fires trigger:
current = original event
for each subscribed runner (filtered by timing):
    outcome, modified = run trigger graph with current payload
    if outcome was "cancel": return null
    current = modified                # graph's post-modification payload
return current
```

Constraints implied:
- "Modify" is one mechanism, not two — whether the framework lets
  graphs edit-in-place or return a replacement is an implementation
  choice; pick whichever is simpler. The host only observes "the
  payload after this graph ran."
- "Cancel" is a distinct outcome from "no modification" — the
  framework already distinguishes these via `Flow.Outcome`.

### UC4 — runner-level + node-level init, both overridable

> When the runner binds an asset, the runner orchestrates the init
> loop and each node decides what its own init looks like. Both
> are individually overridable. The wiring work itself (port
> binding, flow connection setup, runner ref attachment, entry
> registration) is framework-private — consumer overrides add
> setup on top of it, not replace it.

```
# external builder (framework, not on the override surface):
build(runner, asset):
    pre-resolve everything (node-id map, edges, ports)
    create connection objects (data + flow), once each
    hand each node its slice → node.build(runner, slice)
    hand the wired nodes to the runner → runner.initialize(nodes)
    return runner ready

# node side (framework wiring step — virtual, default does the work):
node.build(runner, slice):
    default = attach my incoming data connections to my input ports
              attach my flow connections (out + in) to my flow ports
              capture runner reference (typed nodes only)
    # node owns all its own port mutation; nothing happens to its
    # ports from outside

# node side (consumer hook):
node.initialize(runner):
    default = no-op
    # consumer override adds custom setup using runner services
    # (e.g., pre-cache stuff). No wiring concerns here.

# runner side (loop seam — virtual):
runner.initialize(nodes):
    default = for each node:
                  if entry: register by payload type
                  node.initialize(runner)
    # consumer override can wrap the loop, filter nodes, add
    # cross-cutting work (e.g. service warmup) before/after.
```

Constraints implied:
- Wiring is external (`build`), not on either `Initialize` override
  surface. Consumers can override `runner.initialize` or
  `node.initialize` freely without risk of bypassing wiring.
- Two virtual seams — `runner.initialize` (whole-loop scope) and
  `node.initialize` (per-node scope, consumer hook only). Consumer
  picks the granularity that matches what they're doing.
- No "post-init" phase. Anything a consumer wants to do after
  nodes are ready, they do at the end of their `runner.initialize`
  override, after calling base.

---

## Design decisions (with rationale)

### D1 — Runner *is* the executor

**Decision.** Fold `GraphExecutor` and `GraphController<TRunner>` into
`GraphRunner`. Delete both as standalone types. The runner becomes the
single object the consumer holds.

**Rationale.**

- Consumers conceptually want to hold "the running graph." That's the
  runner — the thing typed to their package, the thing they inject
  services into, the thing they call `Run()` on.
- The audit (S3) made `GraphExecutor` static when it had zero state.
  The moment we want a virtual `ExecuteNode` extension seam, static
  doesn't work. A non-static `GraphExecutor` class would re-introduce
  a separate object with one virtual — bureaucracy.
- `GraphController<TRunner>` was a thin orchestration layer. With
  init on the runner and walk on the runner, controller has nothing
  left to own.
- "Runner = services bag *and* graph orchestration" arguably violates
  SRP. Counter-argument: the alternative is a 3-object dance for a
  1-object concept, and the runner is already the consumer's
  customization seam (subclassed for services + DI). Pragmatism wins.

**Rejected alternative — separate executor with virtuals.**

```csharp
public class GraphExecutor
{
    public virtual Task<Flow> RunFlow<TRunner>(...) { ... }
    protected virtual Task ExecuteNode(RuntimeNode node, Flow flow) => node.Execute(flow);
}

new GraphController<CardEffectRunner>(asset, new CardEffectExecutor());
```

Cleaner SRP, but two extension points instead of one. Consumers now
need to subclass *both* runner (for services) and executor (for
walk wrapping). Doesn't match the "one base class to extend" goal.

### D2 — Inheritance, not delegates, for extension

**Decision.** `ExecuteNode` is a virtual on the runner. No
`UseInterceptor(NodeStep)` delegate API.

**Rationale.**

- "I was expecting just an override method that I could call in a
  parent class, so interceptors wouldn't be required" — the consumer
  already subclasses the runner; one more virtual override is zero
  additional ceremony.
- One interceptor is enough; if multiple are ever needed, the
  consumer composes their own chain inside their override.
- Delegate-based middleware introduces capture + closure allocations
  per call. Virtual call has zero overhead when not overridden.

### D3 — Wiring lives in a static `GraphBuilder`; `Initialize` is consumer-hook only

**Decision.** Topology resolution and connection wiring live in a
static `GraphBuilder` class. Consumers don't see it directly —
they call `GraphBuilder.Build(runner, asset)` (the only public
entry point for setup), which returns the runner ready to dispatch.

Runner and node both have `Initialize` virtuals, but neither does
wiring:

1. `runner.Initialize(IReadOnlyList<RuntimeNode>)` — default loops
   nodes, registers entries by payload type, calls each node's
   `Initialize(runner)` consumer hook. Override to wrap the loop.
2. `node.Initialize(GraphRunner runner)` — default no-op. Pure
   consumer extension point. Override to add per-node setup.

A separate `node.Build(runner, slice)` method (also virtual, but
framework-internal) is what receives the pre-resolved wiring slice
from the builder and attaches connections to the node's own ports.
Consumers don't override `Build` in the typical case — it's part
of the framework surface.

**Rationale.**

- "We need to be able to override both initializes" — both runner
  and node have a virtual `Initialize`.
- "The initialize of the runner is what loops through the nodes
  to initialize" — runner's default IS the loop.
- Static builder satisfies "we should keep the 'hydration' part
  as a private method" by moving it *out* of the runner/node
  entirely — it's framework infrastructure, not on either
  override surface.
- "Kill the double method, Initialize is just calling Hydrate" —
  no Initialize→Hydrate wrapper inside the node. `Initialize` is
  truly empty by default; `Build` is the wiring step.
- O(N+E) instead of O(N·E): builder pre-resolves once, hands each
  node its slice. No recursion, no per-node edge scanning. (See
  Q6, now closed.)
- No more "footgun": `Initialize` has no load-bearing default, so
  forgetting `base.Initialize(...)` doesn't break the graph.
  Consumer overrides are pure additions.
- No post-init phase. If a consumer wants to run code after all
  nodes are initialized, they do it at the end of their
  `runner.Initialize` override after `base.Initialize(nodes)`.
- The runner's loop method is named `Initialize` rather than `Bind`
  because `Bind` is already the verb for port-level wiring
  (`Connection.Bind(input, output)`, `Port.AcceptOutput`,
  `node.BindPort(...)`). Using it as a runner-level verb invited
  collisions in code search and confusion about scope. The setup
  entry on the framework — `GraphBuilder.Build` — keeps a distinct
  verb at the topmost level, so the three verbs read as a
  hierarchy: `Build` (whole graph) → `Initialize` (per-node hook)
  → port-level `Bind` (the actual wiring).

### D4 — `node.Build` owns its own port mutation; builder coordinates

**Decision.** Each node has a virtual `Build(GraphRunner, NodeBuildSlice)`
method that the builder calls during the wiring pass. The slice is
pre-resolved by the builder; the node attaches it to its own ports
via the existing internal port API (`Port.AcceptOutput`,
`FlowOutPort.Connection`, `FlowInPort.Connection`).

The builder creates each `Connection<T>` and `FlowConnection`
exactly once (flow connections are shared between two nodes; the
same instance is included in both nodes' slices). Builder
guarantees the create-once invariant; nodes only attach.

Entry registration (mapping `EntryRuntimeNodeBase` instances by
payload type) lives on the **runner**'s `Initialize` loop, not the
builder or the node — the runner is the one that needs the lookup
for `Run<TEntry>` dispatch.

**Rationale.**

- "Node knows what is there. So he can do the whole mapping."
  — node owns its own port mutation; nothing reaches into its
  ports from outside the class.
- "We are creating the same connection twice" — solved: builder
  pre-resolves and creates each connection object exactly once,
  hands the same instance to both endpoint nodes' Build calls.
- "We can move the building logic to a builder ... can be generic
  and works on every single package" — `GraphBuilder` is a single
  static class shared across all packages. No per-package
  customization for now (see open question for future
  extensibility).
- "No reason for the runtime node to register itself on runner.
  Runner is the one that initialized it — let runner register it."
  — runner's `Initialize` loop pattern-matches `EntryRuntimeNodeBase`
  and registers.
- O(N+E) total cost. No recursion, no per-node edge scanning.
- "Three separate steps" on the node lifecycle: ctor (parameterless,
  for deserialization) → `Build` (framework wiring) → `Initialize`
  (consumer hook). Clear separation of concerns.

### D5 — Payload moves to `Flow`, not to the entry node

**Decision.** Strip `_runFromHere`, `BindRunner(Func)`, `Run(TEntry)`,
`Run(object)`, `BindForRun<TRunner>` *and* `SetPayload` /
`Payload` from `EntryRuntimeNode<TEntry>`. The runner walks; the
typed payload for a given run lives on `Flow`. The entry's `Execute`
reads `flow.GetPayload<TEntry>()`.

**Rationale.**

- `EntryRuntimeNode.Payload` was per-instance mutable state — two
  concurrent `Run` calls on the same runner would clobber each
  other's payload. `Flow` is per-run by construction, so payload
  on Flow is concurrency-safe by default.
- The closure (`_runFromHere`) existed because `GraphExecutor.RunFlow`
  needed runner + asset + scope plumbed through. With the runner
  *being* the executor, those refs are already on `this`.
- The typed `Run(payload)` call site (used by Strike500Tests) moves
  to `runner.Run(payload)`. Host pattern-match-then-Run becomes
  pattern-match-runner-then-Run.
- The entry node now only needs to expose its payload type
  (`PayloadType`) so the runner can route by-type. No mutable state.

**Caveat.** Output ports on auto-generated entry runtimes (e.g.
`OnPlayRuntime.Card`, `Player`) read closures that today capture
`this.Payload`. Those need to capture `flow.GetPayload<TEntry>()`
instead — but output port `Read()` doesn't carry a `Flow`, so this
is a deeper port-system change. See Q7.

### D6 — Generics: keep `GraphAsset<TRunner>`, runner non-generic

**Decision.** Asset stays typed by runner (`CardGraphAsset :
GraphAsset<CardEffectRunner>`). Runner is non-generic. Setup goes
through `GraphBuilder.Build<TRunner>(runner, asset)`, where TRunner
is inferred from the args and runner/asset matching is enforced at
compile time via generic constraints — no runtime cast at the
runner level.

**Rationale.**

- Asset declarations are the user's main "package surface" (they're
  the SO type with `[CreateAssetMenu]`). Self-documenting their
  runner pairing reads better there than on the runner.
- Runner type inversion (`Runner<TGraph>`) was offered by the user
  ("if needed we can invert the generics"). Both work; keeping the
  current direction minimizes diff against existing assets and
  generator emit. Runtime cast in `Initialize` is a one-time check
  at setup, never on the hot path.
- CRTP on the runner (`GraphRunner<TRunner> where TRunner :
  GraphRunner<TRunner>`) would give compile-time cast safety but
  introduces a classic-but-still-jarring type signature for every
  consumer subclass. Skipped unless the runtime cast becomes a real
  problem.

**Note:** Q3 (the inversion alternative) is closed — moot under the
builder shape. See Q3 for details.

---

## Architecture sketch

### File layout

```
Runtime/
  Asset/      GraphAsset.cs                  (unchanged)
  Controller/
    GraphRunner.cs                           (services + dispatch + executor walk)
    GraphBuilder.cs                          NEW — static, wires graphs
    GraphExecutor.cs                         DELETED
    GraphController.cs                       DELETED
  Flow/       Flow.cs                        (carries per-run Payload now)
  Markers/    PortMeta, CatalogEntry, …      (unchanged)
              IEntryBridge.cs                 already deleted (Batch 8)
  Nodes/
    RuntimeNode.cs                           (virtual Build + virtual Initialize)
    EntryRuntimeNode.cs                      (just a type marker — no Payload field)
    OnTrigger.cs                             (reads payload from Flow now)
    Builtin/  …                              (unchanged)
  Ports/      Ports.cs, Connection.cs        (unchanged)
```

### Key types — proposed shape

#### `GraphRunner`

```csharp
public abstract class GraphRunner
{
    // Only persistent state: payload-type → entry node lookup, built
    // by Initialize, used by every Run<TEntry> dispatch.
    Dictionary<Type, EntryRuntimeNodeBase> _entriesByPayload = new();

    // Override seam — the loop. Called by GraphBuilder after wiring;
    // not called by consumer code directly. Default iterates nodes,
    // registers entries, calls each node's Initialize hook.
    public virtual void Initialize(IReadOnlyList<RuntimeNode> nodes)
    {
        foreach (var n in nodes)
        {
            if (n is EntryRuntimeNodeBase entry)
                _entriesByPayload[entry.PayloadType] = entry;
            n.Initialize(this);
        }
    }

    public Task<Flow> Run<TEntry>(TEntry payload, CancellationToken ct = default)
        where TEntry : class
    {
        if (!_entriesByPayload.TryGetValue(typeof(TEntry), out var entry))
            throw new InvalidOperationException($"No baked entry for {typeof(TEntry).FullName}.");

        var flow = CreateFlow(ct);
        flow.SetPayload(payload);
        return RunFlow(entry, flow);
    }

    // Dispatch through a specific named flow-out on the entry, returning
    // the typed result the graph terminated with. Used by package-specific
    // entry bases (e.g. CardEntry<T>.RunValidate(payload)) that expose
    // multi-flow-out shapes.
    protected async Task<TResult?> RunFromFlowOut<TPayload, TResult>(
        TPayload payload, string portName, CancellationToken ct = default)
        where TPayload : class
    {
        if (!_entriesByPayload.TryGetValue(typeof(TPayload), out var entry))
            throw new InvalidOperationException($"No baked entry for {typeof(TPayload).FullName}.");
        if (!entry.Ports.TryGetValue(portName, out var port) || port is not FlowOutPort flowOut)
            throw new InvalidOperationException($"Entry has no flow-out '{portName}'.");

        var flow = CreateFlow(ct);
        flow.SetPayload(payload);

        // Walk starts AT the destination of this flow-out, not the entry.
        var dest = flowOut.Connection?.Destination.Owner;
        if (dest == null) return default;       // unwired flow-out — no work

        await RunFlow(dest, flow).ConfigureAwait(false);
        return flow.ReadResult<TResult>();
    }

    // Public read for hosts that pattern-match — e.g. trigger buses
    // that need to dispatch to a specific entry runtime type.
    public T? GetEntry<T>() where T : EntryRuntimeNodeBase
    {
        foreach (var e in _entriesByPayload.Values)
            if (e is T t) return t;
        return null;
    }

    async Task<Flow> RunFlow(RuntimeNode start, Flow flow)
    {
        RuntimeNode? current = start;
        while (current != null)
        {
            await ExecuteNode(current, flow).ConfigureAwait(false);
            current = flow.ConsumeNext()?.Connection?.Destination.Owner;
        }
        return flow;
    }

    // Override seam — wraps each node execution. Default just calls.
    protected internal virtual Task ExecuteNode(RuntimeNode node, Flow flow)
        => node.Execute(flow);

    // Override seam — game-specific runners attach scope here.
    protected virtual Flow CreateFlow(CancellationToken ct) =>
        new Flow(ct) { Runner = this };
}
```

#### `RuntimeNode`

```csharp
public abstract class RuntimeNode
{
    public int nodeId;
    public string editorGuid = string.Empty;
    [NonSerialized] public readonly Dictionary<string, Port> Ports = new();

    // Framework wiring step — called by GraphBuilder. Default
    // attaches the pre-resolved connections to this node's own
    // ports. Virtual for advanced cases (custom port shapes), but
    // typical nodes don't override.
    internal virtual void Build(GraphRunner runner, in NodeBuildSlice slice)
    {
        foreach (var (dstPortName, srcPort) in slice.IncomingData)
        {
            if (Ports.TryGetValue(dstPortName, out var dst))
                dst.AcceptOutput(srcPort);   // existing typed-cast seam
        }

        foreach (var conn in slice.FlowConnections)
        {
            if (ReferenceEquals(conn.Source.Owner, this))
                conn.Source.Connection = conn;
            if (ReferenceEquals(conn.Destination.Owner, this))
                conn.Destination.Connection = conn;
        }
    }

    // Consumer extension point — called by runner.Initialize after
    // all nodes are wired. Default no-op. Override to add per-node
    // setup using runner services.
    public virtual void Initialize(GraphRunner runner) { }

    public virtual Task Execute(Flow flow) => Task.CompletedTask;
}

public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
{
    [NonSerialized] TRunner? _runner;

    internal sealed override void Build(GraphRunner runner, in NodeBuildSlice slice)
    {
        base.Build(runner, slice);
        _runner = (TRunner)runner;
    }

    public sealed override Task Execute(Flow flow) => Execute(_runner!, flow);
    public abstract Task Execute(TRunner runner, Flow flow);

    public sealed override void Initialize(GraphRunner runner) => Initialize((TRunner)runner);
    public virtual void Initialize(TRunner runner) { }
}
```

```csharp
// The pre-resolved wiring slice the builder hands to each node.
public readonly struct NodeBuildSlice
{
    // dest-port-name on this node → upstream source port (already resolved)
    public IReadOnlyList<KeyValuePair<string, Port>> IncomingData { get; }

    // Flow connections that touch this node's ports (either as source
    // or destination — node decides which side via Owner check).
    public IReadOnlyList<FlowConnection> FlowConnections { get; }

    public NodeBuildSlice(
        IReadOnlyList<KeyValuePair<string, Port>> incomingData,
        IReadOnlyList<FlowConnection> flowConnections)
    {
        IncomingData = incomingData;
        FlowConnections = flowConnections;
    }
}
```

Three-step node lifecycle:

- **ctor** (parameterless): runs at deserialization time. Populates
  the `Ports` dict. No runner / no asset / no connections yet.
- **`Build`** (framework, internal virtual): called by `GraphBuilder`
  during the wiring pass. Default attaches the pre-resolved slice
  to this node's ports + captures the runner ref for typed nodes.
  Typical consumer nodes don't override.
- **`Initialize`** (consumer hook, public virtual): called by
  `runner.Initialize` after every node is wired. Default no-op.
  Override to do per-node setup using runner services.

Entry-registration is *not* here — runner's `Initialize` loop
pattern-matches `EntryRuntimeNodeBase` and registers. The node has
no opinion about whether it's an entry.

#### `GraphBuilder` (static, internal)

```csharp
internal static class GraphBuilder
{
    // The single setup entry point. Consumer constructs the runner
    // (with services) and the asset, calls Build, gets back the
    // runner ready to dispatch.
    public static TRunner Build<TRunner>(TRunner runner, GraphAsset<TRunner> asset)
        where TRunner : GraphRunner
    {
        // 1. Pre-resolve node-id → node map. O(N), one pass.
        var byId = new Dictionary<int, RuntimeNode>(asset.nodes.Count);
        foreach (var n in asset.nodes) byId[n.nodeId] = n;

        // 2. Bucket incoming data connections by destination node-id.
        //    For each data edge, resolve upstream source port once.
        var incomingByNode = new Dictionary<int, List<KeyValuePair<string, Port>>>();
        foreach (var c in asset.connections)
        {
            if (!byId.TryGetValue(c.fromNodeId, out var from)) continue;
            if (!byId.TryGetValue(c.toNodeId, out var to)) continue;
            if (!from.Ports.TryGetValue(c.fromPortName, out var srcPort)) continue;

            if (!incomingByNode.TryGetValue(to.nodeId, out var list))
                incomingByNode[to.nodeId] = list = new();
            list.Add(new(c.toPortName, srcPort));
        }

        // 3. Build flow connections — exactly once each. Bucket by
        //    each touching node-id so both endpoints get the same
        //    instance.
        var flowByNode = new Dictionary<int, List<FlowConnection>>();
        foreach (var e in asset.flowEdges)
        {
            if (!byId.TryGetValue(e.fromNodeId, out var from)) continue;
            if (!byId.TryGetValue(e.toNodeId, out var to)) continue;
            if (!from.Ports.TryGetValue(e.fromPortName, out var src) || src is not FlowOutPort flowOut) continue;
            if (!to.Ports.TryGetValue(e.toPortName, out var dst) || dst is not FlowInPort flowIn) continue;

            var conn = new FlowConnection(flowOut, flowIn);

            if (!flowByNode.TryGetValue(from.nodeId, out var fromList))
                flowByNode[from.nodeId] = fromList = new();
            fromList.Add(conn);

            if (from.nodeId != to.nodeId)
            {
                if (!flowByNode.TryGetValue(to.nodeId, out var toList))
                    flowByNode[to.nodeId] = toList = new();
                toList.Add(conn);
            }
        }

        // 4. Hand each node its slice — node attaches to its own ports.
        foreach (var n in asset.nodes)
        {
            incomingByNode.TryGetValue(n.nodeId, out var inData);
            flowByNode.TryGetValue(n.nodeId, out var flow);

            var slice = new NodeBuildSlice(
                (IReadOnlyList<KeyValuePair<string, Port>>?)inData ?? Array.Empty<KeyValuePair<string, Port>>(),
                (IReadOnlyList<FlowConnection>?)flow ?? Array.Empty<FlowConnection>());

            n.Build(runner, slice);
        }

        // 5. Hand off to runner (registers entries + calls
        //    node.Initialize for each node).
        runner.Initialize(asset.nodes);

        return runner;
    }
}
```

Single pass over `asset.connections` (O(E_data)) and `asset.flowEdges`
(O(E_flow)). Single pass over `asset.nodes` to dispatch slices.
Total cost: O(N + E). Each `Connection<T>` and `FlowConnection`
created exactly once.

#### `EntryRuntimeNode<TEntry>` and `Flow`

```csharp
public abstract class EntryRuntimeNodeBase : RuntimeNode
{
    public abstract Type PayloadType { get; }
}

public abstract class EntryRuntimeNode<TEntry> : EntryRuntimeNodeBase
    where TEntry : class
{
    public override Type PayloadType => typeof(TEntry);

    // No instance payload state. Reads from per-run Flow:
    protected TEntry? GetPayload(Flow flow) => flow.GetPayload<TEntry>();
}
```

```csharp
public sealed class Flow
{
    object? _payload;

    internal void SetPayload(object payload) => _payload = payload;
    public T? GetPayload<T>() where T : class => _payload as T;

    // ... existing Flow members (Outcome, Result, Scope, etc.) ...
}
```

The runner stashes payload on `Flow` once per run:

```csharp
// in GraphRunner.Run<TEntry>:
var flow = CreateFlow(ct);
flow.SetPayload(payload);
return RunFlow(entry, flow);
```

Concurrent `runner.Run` calls each get their own `Flow`, so payload
is per-run by construction — no shared mutable state on the entry.

### Custom entry shapes (per-package + per-type)

Per the user's preference for inheritance: each package defines a
**base entry class** that declares its flow-out shape. Per-type
entries inherit and add nothing. The generator emits the per-payload
subclass; the package's base is hand-written.

```csharp
// Package-side hand-written base (lives in CardEffect runtime asm):
public abstract class CardEntry<TPayload> : EntryRuntimeNode<TPayload>
    where TPayload : class
{
    public FlowOutPort Validate = null!;
    public FlowOutPort Execute  = null!;

    protected CardEntry()
    {
        Validate = new FlowOutPort(this, nameof(Validate));
        Execute  = new FlowOutPort(this, nameof(Execute));
        Ports.Add(Validate.Name, Validate);
        Ports.Add(Execute.Name,  Execute);
    }
}

// [GraphPackage] declares the base. NOTE: `EntryBase` is a NEW property
// being added to the existing GraphPackage attribute as part of this
// redesign — it doesn't exist on [GraphPackage] today.
[assembly: GraphPackage(
    Runner    = typeof(CardEffectRunner),
    Extension = "card",
    AssetMenu = "GraphFlow/Card",
    EntryBase = typeof(CardEntry<>))]

// Generator emits per-payload subclass:
[Serializable]
public sealed class OnPlayRuntime : CardEntry<OnPlay>
{
    public OutputPort<Card> Card;
    public OutputPort<PlayerId> Player;
    // ctor wires data-output ports as before
}
```

#### Typed dispatch — canonical path

The canonical way to dispatch through a specific flow-out is via the
package's entry base, reached through `runner.GetEntry<T>()`:

```csharp
// On the package's entry base — written once per package:
public abstract class CardEntry<TPayload> : EntryRuntimeNode<TPayload>
    where TPayload : class
{
    // ... ports as above ...

    public Task<bool> RunValidate(TPayload payload) =>
        Runner!.RunFromFlowOut<TPayload, bool>(payload, nameof(Validate));

    public Task RunExecute(TPayload payload) =>
        Runner!.RunFromFlowOut<TPayload, object>(payload, nameof(Execute));
}

// Host call site:
var entry = runner.GetEntry<OnPlayRuntime>()!;
if (!await entry.RunValidate(new OnPlay { ... })) return;
await entry.RunExecute(new OnPlay { ... });
```

(Implies `Runner` is exposed on `EntryRuntimeNode<TEntry>` — readable
by the entry's own typed-dispatch methods. Captured during `Build`.)

#### Typed dispatch — optional sugar on the runner subclass

A consumer who finds the `GetEntry<T>()` indirection chatty can add
package-specific helpers on their runner subclass that wrap the same
underlying primitive. Pure sugar; no new framework concept:

```csharp
public sealed class CardEffectRunner : GraphRunner
{
    public Task<bool> RunValidate<T>(T payload) where T : class =>
        RunFromFlowOut<T, bool>(payload, "Validate");

    public Task RunExecute<T>(T payload) where T : class =>
        RunFromFlowOut<T, object>(payload, "Execute");
}

// Host call site (sugar):
if (!await runner.RunValidate(new OnPlay { ... })) return;
await runner.RunExecute(new OnPlay { ... });
```

Both paths bottom out in the same `GraphRunner.RunFromFlowOut<...>`
primitive. Q1 captures the call-site trade-off.

---

## Open questions / nitpicks

### Q1 — Where does `RunValidate` / `RunExecute` live?

**Resolution: both, with (2) canonical and (1) optional sugar.** All
paths bottom out in the same `GraphRunner.RunFromFlowOut<TPayload, TResult>`
primitive. The choice is just where the call-site sugar lives.

1. **On the runner subclass** (e.g. `CardEffectRunner.RunValidate<T>`).
   Optional package-level sugar. Call site:
   `await runner.RunValidate(new OnPlay { ... })`. Per-package — a
   consumer who doesn't want this sugar simply doesn't write it.

2. **On the per-package entry base** (e.g.
   `CardEntry<TPayload>.RunValidate(TPayload)`). **Canonical.**
   Reached via `runner.GetEntry<OnPlayRuntime>()!.RunValidate(payload)`.
   The typed-dispatch surface lives in one place per package; every
   entry that inherits the base gets it for free.

3. **On the generated entry subclass** (e.g.
   `OnPlayRuntime.RunValidate(OnPlay)`) — same call shape as (2),
   but the methods are emitted per-entry by the generator. We're not
   pursuing this — duplicates the surface.

The doc's example shows both side-by-side. Pick the one that matches
the consumer's ergonomic preference for that package; they're not
mutually exclusive.

### Q2 — Constrain flow-out result types (validator)

Card game wants `Validate` to terminate at `Return<bool>`. Today,
EFG-V04 only checks "flow path terminates at Return/Cancel" — no type
match. Adding per-port expected-type metadata:

- Where declared? On the FlowOutPort field in the package's entry
  base — attribute? `[FlowOutResult(typeof(bool))] public FlowOutPort
  Validate;` — generator/registry reads, validator enforces.
- Or on the runner's `RunFromFlowOut` declaration — the typed
  `Task<TResult>` return tells you `TResult`.
- Or both — declarative on the port for the validator + reflective
  on the call site for the runner.

To decide. Probably attribute-on-port for clarity.

### Q3 — ~~Generic direction (D6 vs D6-alt)~~ (CLOSED)

Originally open as a trade-off between `GraphAsset<TRunner>` (current)
and `GraphRunner<TGraph>` (inverted). Mostly moot under the static-
builder shape: `GraphBuilder.Build<TRunner>(TRunner runner, GraphAsset<TRunner> asset)`
enforces runner/asset matching at compile time via generic inference,
so the runner-side runtime cast that motivated the question doesn't
exist. The remaining runtime cast — `(TRunner)runner` inside
`RuntimeNode<TRunner>.Build` — is unaffected by the inversion either
way.

Closed in favor of keeping `GraphAsset<TRunner>`. Re-open if a future
need (e.g., a non-generic asset shape) surfaces.

### Q4 — Multiple `Build` or single?

`GraphBuilder.Build` has no idempotency guard. Calling it twice
on the same runner would re-wire ports (clobbering the first
wiring), re-create connections, and re-populate
`_entriesByPayload`. Almost certainly wrong.

Options:

1. Guard with a flag on the runner — throw on second `Build`.
2. Allow re-Build as a feature (asset swapping). Reset
   `_entriesByPayload`, walk nodes to clear port connections,
   then re-wire.
3. Don't guard — document that calling twice is undefined.

Lean: (1). Re-build is a feature we don't have a use case for.
Cheap: just a `bool _built` on the runner that `Build` sets.

### Q5 — Trigger modify mechanism (UC3)

The user clarified UC3: graphs need a way to modify the inflight
trigger payload, but whether that's edit-in-place or replace-by-
value is "whatever's easier."

Two sketches:

1. **Edit-in-place** — `OnTrigger<TEvent>.Event` is writable; nodes
   that take the trigger as a data input + write back mutate the
   live payload. Host reads
   `flow.GetPayload<OnTrigger<TEvent>>()?.Event` after the run.
2. **Replace-by-Return** — graph terminates at `Return<TEvent>`;
   host reads `flow.ReadResult<TEvent>()`.

Pick one for the framework convention. Both are nearly free
(framework already has the primitives); the choice is which idiom
we *document* and use in samples.

Lean: edit-in-place — terser graph authoring (no need for an
explicit `Return` node at the end of every modifier graph),
simpler host code.

### Q6 — ~~Init perf at scale~~ (CLOSED)

The recursive-self-init concern (`O(N · E)`) is moot under the
builder design. `GraphBuilder.Build` is a single O(N + E) pass:
one walk of `asset.connections`, one walk of `asset.flowEdges`,
one walk of `asset.nodes` to dispatch slices. Each `Connection<T>`
and `FlowConnection` created exactly once.

### Q7 — Concurrent runs and output-port closures

D5 fixes payload concurrency by moving `Payload` from the entry
node to `Flow`. But the auto-generated entry runtime classes today
expose payload data via output ports whose `Read()` closures
capture `this.Payload`:

```csharp
public sealed class OnPlayRuntime : EntryRuntimeNode<OnPlay>
{
    public OutputPort<Card> Card;
    public OutputPort<PlayerId> Player;

    public OnPlayRuntime()
    {
        Card   = new OutputPort<Card>(() => Payload!.Card);          // captures `this`
        Player = new OutputPort<PlayerId>(() => Payload!.Player);
    }
}
```

`Payload` is gone from the node, and the closure has no `Flow`
parameter to thread through. So downstream nodes reading entry
data ports lose their value source.

Options:

1. **Per-run output-port reads** — change `OutputPort<T>.Read()`
   to take a `Flow`. Big port-system change; touches every node
   that constructs OutputPort<T>.
2. **Thread/run-local Flow** — stash the active Flow in an
   `AsyncLocal<Flow>` so closures can read it. Magic; surprises
   debuggers and analyzers; works for true async but iffy under
   sync-over-async.
3. **One runner per concurrent path** — accept that running the
   same runner twice in parallel is undefined, document, move on.
   Consumers spin up a runner per concurrent dispatch (cheap
   given the new Initialize-on-runner shape).

Lean: (3) for now. Concurrency on the same runner isn't a real
use case the package needs to support today. (1) is the right
long-term answer if it ever becomes one.

> ⚠ **Conflict flagged in Tenets.** The "concurrent runs on the
> same runner are expected to work" tenet contradicts this lean.
> Q7 needs a deeper revisit; deferred to a separate pass.

### Q8 — `NodeBuildSlice` parameter shape

**Resolution: single `IReadOnlyList<FlowConnection>` for flow,
node self-detects which side via `Owner` ref check.** Picked over
the alternative ("split into two dicts: outgoing-by-source-port-name
+ incoming-by-dest-port-name") for simpler builder bookkeeping.
Per-node cost is one extra `ReferenceEquals(conn.Source.Owner, this)`
check per flow connection, which is trivial.

The alternative would be more explicit (no self-check needed) but
doubles the dictionary count in the builder and adds two-dict
construction per node. Both work; chose the lighter shape.

**Single hard-cutover commit** ("refactor(GraphFlow): runner =
executor + dispatch, static GraphBuilder owns wiring"). Old
`GraphController` / `GraphExecutor` are deleted in the same commit
that introduces `GraphBuilder` + the new runner shape. No
"alive-but-obsolete" transition window — every consumer call site
moves in lockstep. Generator output for runtime nodes is unchanged;
the registry's `CreateBridge` → `BindForRun` plumbing already
changed in Batch 8.

The cutover is feasible in one commit because:

- Consumer surface change is mechanical: `new GraphController<T>(asset); controller.Initialize(runner); controller.Run(...)` →
  `GraphBuilder.Build(runner, asset); runner.Run(...)`.
- All three call-site updaters (`CardSandbox`, `M0Sandbox`,
  `Tests/RuntimeSmokeTests`) are in the same repo and ride the same
  commit.

Order of work within the commit:

1. **Add `GraphRunner` API** — `Initialize(IReadOnlyList<RuntimeNode>)`
   virtual + `Run<TEntry>` + `RunFromFlowOut<TPayload, TResult>` +
   `GetEntry<T>` + `ExecuteNode` virtual + `CreateFlow` virtual.
   `_built` guard.
2. **Add per-run payload to `Flow`** — `SetPayload` (internal) and
   `GetPayload<T>()` (public). Runner stashes payload before walk.
3. **Add `RuntimeNode.Build(runner, slice)`** — internal virtual,
   default attaches the slice to local ports. Add `NodeBuildSlice`
   record. `Initialize(runner)` becomes a no-op consumer hook.
4. **`RuntimeNode<TRunner>`** — seal `Build` to also capture
   `_runner`; seal `Initialize(GraphRunner)` to forward to typed
   `Initialize(TRunner)`.
5. **Add `GraphBuilder.cs`** — single `Build<TRunner>(runner, asset)`
   method. O(N+E) wiring, then calls `runner.Initialize(nodes)`.
6. **Strip `EntryRuntimeNode<T>`** — remove `_runFromHere`, `Run`,
   `BindForRun`, `Run(object)`, `SetPayload`, `Payload`. Just
   `PayloadType` + a getter that reads from Flow.
7. **Update generator** — `EntryBase` property added to the
   `[GraphPackage]` attribute; per-payload subclass emit closes
   `EntryBase<TPayload>`; output port closures capture `flow`
   instead of `Payload` (deferred — see Q7).
8. **Delete** `GraphController.cs`, `GraphExecutor.cs`. Migrate
   samples + `Tests/RuntimeSmokeTests` call sites.
9. **Run** snapshot tests + smoke tests; refresh snapshots if
   generator output shifts.

Estimated net delete: ~200 lines of framework (controller +
executor + EntryRuntimeNode complexity), plus a ~80-line static
`GraphBuilder.cs` add. Net negative. Plus generator changes for
`EntryBase` and per-run output-port closures.

---

## Test impact

- **Snapshot tests** — `HarnessGraphRegistry.g.cs` registry shape
  may change if `BindForRun` plumbing is removed; new attribute
  `EntryBase` adds emit branches. Snapshots regenerated post-change.
- **`Tests/RuntimeSmokeTests`** — fixtures use `controller.Initialize`
  + `controller.Run`. Migrate to `GraphBuilder.Build(runner, asset)`
  + `runner.Run`. Trivial.
- **`CardSandbox/Tests/Strike500Tests`** — uses `trig.Run(payload)`
  for trigger pattern-match. Migrate to keeping a runner reference
  per subscribed entry and calling `runner.Run(payload)`.
- **Editor / generator** — `RegisterAdditional` partial on
  `<Stem>GraphRegistry` is unchanged. The per-package OnTrigger /
  Return shims are unchanged.
- **`PortValueResolver`** — unchanged (bake-time, doesn't touch
  controller/executor).

---

## What this enables (besides UC1-UC4)

- Replay / determinism harnesses can subclass the runner, override
  `ExecuteNode` to record/replay node outputs.
- Profiler integration via the `ExecuteNode` seam — wrap each call
  in a `BeginSample`/`EndSample`.
- Conditional gating ("if game is paused, await before continuing
  the walk") drops in cleanly at `ExecuteNode`.
- Cross-graph state sharing — multiple runners can ref one services
  instance via DI, runner subclasses just hold the ref.

---

## Future considerations (out of scope for this redesign)

Things we discussed and intentionally deferred. Captured here so
they don't get lost.

- **Runner ref via Run / Execute parameters** instead of a cached
  `_runner` field on `RuntimeNode<TRunner>`. Today the runner ref
  is captured once during `Build` and read on every `Execute`. An
  alternative is to thread the runner through `ExecuteNode` /
  `node.Execute` parameters directly — eliminates the cached field,
  makes the per-call surface explicit, and removes one spot where
  runner-state mutation could drift across concurrent runs.
  Trade-off: more verbose call sites; every node body reads
  `runner.Foo` instead of `_runner.Foo`. Fine for now to keep the
  cached field; revisit if concurrent-run guarantees push us
  toward fully stateless nodes.

- **Per-package builder customization** — currently `GraphBuilder`
  is a single static class shared across all packages. If a package
  ever needs a different wiring strategy (custom port resolution,
  alternative connection materialization), promote `GraphBuilder`
  to a class with virtuals and add a
  `protected virtual GraphBuilder CreateBuilder()` on `GraphRunner`.
  Not worth the API surface today.

---

## Anti-goals (what this redesign explicitly does **not** do)

- **No runtime reflection added.** Type matching at setup is
  compile-time via generic inference. The one runtime cast is
  `RuntimeNode<TRunner>.Build`'s `(TRunner)runner` when capturing
  the typed runner ref — once per node at Build time, never on the
  hot path.
- **No new attributes on user payloads** — entry shapes come from
  the package's hand-written entry base, not from a forest of
  attributes per payload.
- **No DI container required** — runner ctor is whatever the user
  wants; the framework doesn't impose service injection patterns.
- **No async-over-sync** — the walk is genuinely async (nodes can
  await services), no busy loops or sync wrappers.
