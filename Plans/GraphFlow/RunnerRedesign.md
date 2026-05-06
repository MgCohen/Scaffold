# GraphFlow Runner Redesign — Plan & Sketches

Status: **Draft v2 — shape locked after a review pass that closed all
implementation-blocking questions. Ready to implement.**

This doc replaces the v1 sketch. The shape changed substantially after a
design pass that pulled the model closer to FlowCanvas / Unreal Blueprint
idioms (pull-based dataflow + push-based execution, per-In handlers,
flow-keyed caches) while keeping the framework's existing strengths
(typed ports, source-generated catalogs, no reflection at runtime).

A review pass after the v2 first draft resolved ten outstanding details
that were ambiguous or under-specified. Those resolutions are integrated
inline (file layout, port primitives, Flow shape, GraphRunner, built-in
node sketches, OnTrigger handling, migration steps). The summary table
of resolved details lives in **§ "Resolved during v2 review pass"**
below; each decision is also reflected at the relevant point in the
architecture.

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

## Resolved during v2 review pass

The first draft of v2 left several details either implicit or open. A
review pass closed each of them. The decisions below are already
threaded through the architecture sections; this table is an at-a-glance
index so the rationale for any sketch detail is one lookup away.

| # | Topic | Decision |
|---|---|---|
| R1 | `Connection<T>` (abstract `Connection` and the data-edge wrapper) | **Deleted.** `InputPort<T>` holds an `OutputPort<T>` directly; reads go straight to the source. `FlowConnection` survives because the walk loop traverses it. |
| R2 | `OnTrigger<TEvent>.Event` mutable field | **Replaced with `OutputPort<TEvent> Event`.** The inner event lives in a separate `Inner` field on the payload-wrapper instance; the port reads it via `flow.GetPayload<…>().Inner`. No mutable per-run state on the shared node. |
| R3 | `EntryRuntimeNodeBase.GetDefaultOut()` for `Run<TEntry>` dispatch | **Concrete on the base.** Scans `Ports` for the sole `FlowOutPort` and returns it; throws if zero or more than one. Multi-out entries (e.g., `CardEntry`) trip the throw and force callers to use `RunFromFlowOut`. |
| R4 | `NodeBuildSlice` shape | **Pre-resolved port pairs.** `Bake` walks the asset's name-keyed dicts once and hands each node a `(IReadOnlyList<DataBinding> data, IReadOnlyList<FlowBinding> flow)` slice. `Build` just calls `inputPort.Connect(outputPort)` per data entry and assigns `FlowConnection` per flow entry. Generic-erasing `DataBinding` carries an `Action<>` apply step (one per pair) closed over the typed pair at bake time. |
| R5 | `RuntimeNode.Bind()` and `Port.AcceptOutput()` | **Both deleted.** Test fixtures and any hand-authored wiring call `inputPort.Connect(outputPort)` directly. The slice-based `Build` is the only production wiring path. |
| R6 | `FlowOutPort` / `FlowInPort` ctor signature | **Positional, immutable.** `new FlowOutPort(owner, name)` / `new FlowInPort.Sync(owner, name, handler)` etc. (object-initializer style was sketched in the v2 first draft; rejected — generator already emits positional, immutable Owner/Name is safer.) |
| R7 | `FlowPool` for per-Run allocation | **None for now.** `new Flow(payload, ct, runner)` per dispatch. Pooling stays a documented future optimisation; deferring it shrinks the cutover diff and the GC delta of one Flow per Run is small. |
| R8 | `ICacheClearable` interface | **None.** `Port` base gets a `internal virtual void ClearCache(Flow flow) { }` no-op. `OutputPort<T>` overrides it. `Flow.InvalidateAll` walks `_touched` and calls `port.ClearCache(this)` directly. |
| R9 | `FlowInPort` sync vs async ctor overload resolution (Q-async-overload) | **Named static factories.** `FlowInPort.Sync(owner, name, Func<Flow, FlowOutPort>)` and `FlowInPort.Async(owner, name, Func<Flow, Task<FlowOutPort>>)`. **Async return type is `Task<FlowOutPort>`, not `ValueTask<FlowOutPort>`.** Eliminates lambda-binding ambiguity entirely; intent is explicit at the call site. |
| R10 | `GraphAsset` shape for `GraphTopology.Bake` | **Non-generic `GraphAsset` base.** Holds the `nodes` / `connections` / `flowEdges` lists. `GraphAsset<TRunner>` becomes a typed marker subclass. `Bake` is runner-agnostic and takes the non-generic base; `GraphBuilder<TRunner>.Build(GraphAsset<TRunner>)` keeps type-safety at the call site. |

A second review pass after the first resolution batch closed seven more
points that surfaced during a pre-implementation readiness audit. They
extend the table:

| # | Topic | Decision |
|---|---|---|
| R11 | Non-generic `GraphAsset` instantiability | **Abstract.** Cannot be created without a runner-typed subclass. Preserves the "every asset is bound to a runner type" invariant; `Bake` consumes it as the framework-side seam, callers always go through `GraphAsset<TRunner>`. |
| R12 | `Bake` and node instance ownership | **Shared instances.** `Bake` does **not** clone nodes — it wires the asset's existing deserialized `RuntimeNode` instances. All runners built from one asset share those instances (per D5). The "no per-runner state on nodes" invariant is enforced by code review, not by per-runner clones. |
| R13 | Parameterless `Return` node | **Kept.** Two `Return` shapes ship: `Return<TResult>` (sets Outcome.Returned with a typed value) and `Return` (sets Outcome.Returned with no result — distinct from `Cancel`'s abort semantics). `Flow` gains a parameterless `Return()` overload returning `FlowOutPort.End`, alongside the typed `Return<T>(T value)`. |
| R14 | `FlowInPort` default name | **`"In"`.** Convention switches from the legacy `"FlowIn"` to `"In"`, matching the FlowOut convention (`Out`, `True`, `False`, `Body`, `Done`, etc.). This breaks any saved `GraphAsset`s that reference `"FlowIn"` as a flow-edge destination port name — acceptable because the cutover already invalidates baked assets (controller deleted, Execute deleted, OnTrigger reshape). All built-in nodes declare their flow-in field as `In` and pass `nameof(In)` to the factory. |
| R15 | Per-payload entry runtime emission | **Complete classes inheriting per-package entry base.** Generator emits each `XxxRuntime` as a full class deriving from the package's hand-written entry base (e.g. `OnPlayRuntime : CardEntry<OnPlay>`). The base declares the flow outs (`Validate`, `Execute`, etc.) once per package; the generator emits only the per-field `OutputPort<T>` data ports closing over `flow.GetPayload<T>()!.FieldX`. No per-payload hand-authoring. |
| R16 | `Bake` data-edge wiring seam | **`internal abstract void ConnectFrom(Port output)` on `InputPort<T>`.** R5 deleted the public `AcceptOutput` because hand-written wiring should go through the typed `inputPort.Connect(outputPort)`. But `GraphTopology.Bake` is framework-internal and needs a generic-erasing seam — adding a non-generic abstract `ConnectFrom` on `InputPort<T>` (overridden once by the generic class to do the typed cast + assignment) gives Bake one virtual call per data edge with zero reflection at bake time. The public hand-written API stays `inputPort.Connect(outputPort)`; Bake uses `ConnectFrom`. Both end at the same `_source = ...` assignment. |
| R17 | `Wait` and `Loop` built-ins | **Both ship in the cutover commit.** `Wait` validates `FlowInPort.Async` + cancellation-token threading; `Loop` validates `flow.GetSlot/SetSlot`, `InvalidateAll`, and the `cache: false` opt-out. None of those APIs have other coverage in the existing built-in set. Step 12 of the migration plan lists both. |

The original v2 open-question list (Q-modify, Q-walk-interception,
Q-pool, Q-async-overload, Q-default-out) is updated below — Q-pool /
Q-async-overload / Q-default-out are now resolved (R7 / R9 / R3
respectively). Q-modify and Q-walk-interception remain genuinely
deferred.

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
In = FlowInPort.Sync(this, nameof(In),
    flow => Condition.Read(flow) ? True : False);
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
  Asset/      GraphAsset.cs                  Non-generic base added (R10) holding
                                             nodes / connections / flowEdges lists.
                                             GraphAsset<TRunner> becomes a typed
                                             marker subclass.
  Builder/
    GraphBuilder.cs                          NEW — abstract<TRunner>, owns bake cache
    BakedGraph.cs                            NEW — wired-graph DTO
    GraphTopology.cs                         NEW — internal Bake() pass
    GraphRunner.cs                           Executor walk + dispatch + entry lookup.
                                             No Flow pool — each dispatch news up
                                             a Flow (R7).
    GraphController.cs                       DELETED
    GraphExecutor.cs                         DELETED
  Flow/       Flow.cs                        Payload + slots + touched-list +
                                             InvalidateAll. No pool, no
                                             ResetForReuse, no GoTo / Stop /
                                             ConsumeNext (R7).
  Markers/    PortMeta, CatalogEntry, …      (unchanged for now)
  Nodes/
    RuntimeNode.cs                           Build(slice) framework wiring +
                                             Initialize(runner) consumer hook.
                                             No Execute, no Bind, no AcceptOutput
                                             (R5).
    RuntimeNode_TRunner.cs                   Typed base + protected static
                                             Runner(Flow) helper.
    EntryRuntimeNode.cs                      Base + GetDefaultOut() concrete
                                             scan-and-throw (R3); typed
                                             EntryRuntimeNode<TPayload> adds
                                             OutputPort<TPayload> Payload (D8).
    OnTrigger.cs                             Reshaped — Event becomes
                                             OutputPort<TEvent>; inner event
                                             lives on the payload-wrapper
                                             instance as `Inner` (R2).
    Builtin/  …                              Rewritten — see below.
  Ports/
    Port.cs                                  Base. Adds internal virtual
                                             ClearCache(Flow) no-op (R8) +
                                             internal virtual ConnectFrom(Port)
                                             throwing default (R16). Owner /
                                             Name remain only on flow ports
                                             (no migration onto base).
    InputPort.cs                             Holds OutputPort<T>? _source
                                             directly. IsConnected,
                                             Connect(OutputPort<T>) (R1),
                                             ConnectFrom(Port) override for
                                             the bake-side seam (R16).
                                             Read(Flow) — no fallback lambda.
    OutputPort.cs                            Func<Flow, T> compute, flow-keyed
                                             cache, cache=false opt-out flag.
                                             Overrides Port.ClearCache.
    FlowInPort.cs                            No public ctor; named static
                                             factories Sync / Async.
                                             Invoke is Func<Flow, Task<FlowOutPort>>
                                             internally; Sync wraps with
                                             Task.FromResult (R9).
    FlowOutPort.cs                           .End static sentinel.
                                             Positional ctor (owner, name) (R6).
    Connection.cs                            Slimmed — only FlowConnection
                                             remains. Abstract Connection +
                                             generic Connection<T> deleted (R1).
```

### Port primitives

```csharp
public abstract class Port
{
    // Cache-clear hook for Flow.InvalidateAll. Default no-op so InputPort
    // and the flow ports inherit the right behavior; OutputPort<T>
    // overrides. Replaces the v1 sketch's ICacheClearable interface (R8).
    internal virtual void ClearCache(Flow flow) { }

    // Generic-erasing wiring seam used by GraphTopology.Bake (R16).
    // Default throws — only InputPort<T> overrides. Output / flow ports
    // never receive a ConnectFrom call from Bake (Bake reads them as the
    // *source* side of an edge, not the destination).
    internal virtual void ConnectFrom(Port output) =>
        throw new InvalidOperationException(
            $"ConnectFrom is only valid on InputPort<T>; got {GetType()}.");
}

public sealed class OutputPort<T> : Port
{
    readonly Func<Flow, T> _compute;
    readonly Dictionary<Flow, T> _cache = new();
    readonly bool _shouldCache;

    public OutputPort(Func<Flow, T> compute, bool cache = true)
    {
        _compute     = compute;
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

    internal override void ClearCache(Flow flow) => _cache.Remove(flow);
}

// InputPort holds an OutputPort<T> directly — Connection<T> deleted (R1).
//
// Two wiring entry points (R16):
//   - `Connect(OutputPort<T>)`: typed, used by hand-written wiring & tests.
//   - `ConnectFrom(Port)`: non-generic seam, used by `GraphTopology.Bake`
//     so it can wire from the asset's name-keyed `Ports` dict (where
//     ports are stored as `Port` base) without reflection. Throws on
//     a type mismatch — same contract as the old `AcceptOutput` had.
public sealed class InputPort<T> : Port
{
    OutputPort<T>? _source;
    public bool IsConnected => _source != null;
    public T Read(Flow flow) => _source is null ? default! : _source.Read(flow);
    internal void Connect(OutputPort<T> source) => _source = source;

    internal override void ConnectFrom(Port output)
    {
        if (output is not OutputPort<T> typed)
            throw new InvalidOperationException(
                $"Bake: output port {output.GetType()} does not match input port InputPort<{typeof(T)}>.");
        _source = typed;
    }
}

public sealed class FlowOutPort : Port
{
    // Sentinel returned by handlers / Return / Cancel to terminate the walk.
    // Owner is null on the sentinel; the walk loop checks ReferenceEquals
    // before dereferencing Connection.
    public static readonly FlowOutPort End = new(null!, "<end>");

    public RuntimeNode    Owner { get; }
    public string         Name  { get; }
    public FlowConnection? Connection { get; internal set; }

    public FlowOutPort(RuntimeNode owner, string name)
    {
        Owner = owner;
        Name  = name;
    }
}

// Sync handlers return a FlowOutPort directly. Async handlers return
// Task<FlowOutPort> (Task, not ValueTask — R9). Internally both shapes
// collapse to a single Task-returning Invoke so the walk loop awaits one
// thing.
public sealed class FlowInPort : Port
{
    public RuntimeNode    Owner { get; }
    public string         Name  { get; }
    public FlowConnection? Connection { get; internal set; }
    internal Func<Flow, Task<FlowOutPort>> Invoke { get; }

    FlowInPort(RuntimeNode owner, string name, Func<Flow, Task<FlowOutPort>> invoke)
    {
        Owner  = owner;
        Name   = name;
        Invoke = invoke;
    }

    // R9 — named factories. No public ctor; lambda binding is unambiguous
    // because the user picks the factory.
    public static FlowInPort Sync(
        RuntimeNode owner, string name, Func<Flow, FlowOutPort> handler) =>
        new(owner, name, flow => Task.FromResult(handler(flow)));

    public static FlowInPort Async(
        RuntimeNode owner, string name, Func<Flow, Task<FlowOutPort>> handler) =>
        new(owner, name, handler);
}
```

**Notes.**

- `Owner` / `Name` live only on the flow ports (matches today). Putting
  them on `Port` base was sketched in the v2 first draft and rejected —
  data ports never need them, and keeping the base minimal keeps the
  generator's per-port allocation footprint flat.
- `OutputPort<T>` is sealed so `ClearCache` is a single direct call; no
  `internal sealed override` ceremony. Same for `InputPort<T>` /
  flow ports.

### `Flow`

```csharp
public enum Outcome { Running, Returned, Cancelled }

public sealed class Flow
{
    readonly object _payload;
    Dictionary<object, object>? _slots;        // per-(node, key) per-flow state
    readonly List<Port> _touched = new();
    object? _result;

    public GraphRunner       Runner { get; }
    public CancellationToken Token  { get; }

    public Outcome Outcome { get; private set; } = Outcome.Running;
    public bool    IsCancelled   => Outcome == Outcome.Cancelled;
    public bool    IsTerminating => Outcome != Outcome.Running;

    // R7 — no pool. New Flow per dispatch. Ctor takes everything;
    // payload / runner / token are immutable for the lifetime of the run.
    internal Flow(object payload, GraphRunner runner, CancellationToken token)
    {
        _payload = payload;
        Runner   = runner;
        Token    = token;
    }

    // Payload
    public T? GetPayload<T>() where T : class => _payload as T;

    // Outcome-setting helpers — return End so handlers compose into
    // one-line lambdas: `flow => flow.Return(Value.Read(flow))`.
    // Two Return overloads (R13): typed for `Return<T>` nodes, no-arg
    // for the parameterless `Return` node — both set Outcome.Returned;
    // the no-arg form leaves _result null. Distinct from `Cancel`,
    // which signals abort, not a successful done.
    public FlowOutPort Return<T>(T value)
    {
        _result = value;
        Outcome = Outcome.Returned;
        return FlowOutPort.End;
    }
    public FlowOutPort Return()
    {
        _result = null;
        Outcome = Outcome.Returned;
        return FlowOutPort.End;
    }
    public FlowOutPort Cancel()
    {
        Outcome = Outcome.Cancelled;
        return FlowOutPort.End;
    }
    public T? ReadResult<T>() => _result is T t ? t : default;

    // Cache touch tracking — direct virtual call (R8), no ICacheClearable.
    internal void RegisterTouched(Port p) => _touched.Add(p);
    public   void InvalidateAll()
    {
        foreach (var p in _touched) p.ClearCache(this);
        _touched.Clear();
    }

    // Slots — per-(owner, this) per-flow scoped state (loop counters etc.).
    // Owner is typically `this` from inside a node (so concurrent runs of
    // the same Loop node don't collide).
    public T GetSlot<T>(object owner) =>
        _slots != null && _slots.TryGetValue(owner, out var v) ? (T)v : default!;
    public void SetSlot<T>(object owner, T value) =>
        (_slots ??= new())[owner] = value!;
}
```

**What's gone vs. v1 / earlier v2 sketches.**

- No `GoTo` / `Stop` / `ConsumeNext` — handlers return `FlowOutPort`
  directly; `FlowOutPort.End` terminates the walk.
- No `ResetForReuse` — there's no pool to reset for (R7).
- No `Scope` — flow context is just payload + runner + token + slots.
- `_nextPort` field is gone (the walk loop holds it on the stack).

### `RuntimeNode` and `RuntimeNode<TRunner>`

```csharp
public abstract class RuntimeNode
{
    public int    nodeId;
    public string editorGuid = string.Empty;
    [NonSerialized] public readonly Dictionary<string, Port> Ports = new();

    // Framework wiring step — called once by Bake. Default applies the
    // slice in a tight loop. Nodes don't typically override this;
    // overriding is left in for nodes that need post-wire fixup.
    //
    // Bind / AcceptOutput from the old API are gone (R5) — the slice is
    // the only wiring path.
    internal virtual void Build(in NodeBuildSlice slice)
    {
        for (int i = 0; i < slice.Data.Count; i++) slice.Data[i].Apply();
        for (int i = 0; i < slice.Flow.Count; i++)
        {
            var f = slice.Flow[i];
            f.Source.Connection      = f.Connection;
            f.Destination.Connection = f.Connection;
        }
    }

    // Consumer extension point — called by the builder after every node
    // is wired. Default no-op. Override to subscribe to events, cache
    // derived state using runner services.
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

- **ctor** (parameterless): runs at deserialization or instantiation via
  the catalog factory. Populates the `Ports` dict and wires per-In
  handlers via the FlowInPort.Sync / FlowInPort.Async factories.
- **`Build`** (framework-internal): called by `Bake` once per node. Walks
  the pre-resolved slice and applies it.
- **`Initialize`** (consumer hook): called by the builder after all
  nodes are wired. Default no-op.

### `NodeBuildSlice` (R4)

`Bake` resolves all port-name lookups and type-pair matching once,
then hands each node a slice carrying ready-to-apply bindings. Nodes
don't see asset edge structs or do dict lookups during Build.

```csharp
public readonly struct NodeBuildSlice
{
    public IReadOnlyList<DataBinding> Data { get; }
    public IReadOnlyList<FlowBinding> Flow { get; }

    public NodeBuildSlice(IReadOnlyList<DataBinding> data,
                          IReadOnlyList<FlowBinding> flow)
    {
        Data = data;
        Flow = flow;
    }
}

// Generic-erased data binding. Bake resolves the typed pair and closes
// over the typed Connect call in `Apply`. This sidesteps having to make
// the slice itself generic over T.
public readonly struct DataBinding
{
    readonly Action _apply;
    public DataBinding(Action apply) { _apply = apply; }
    public void Apply() => _apply();
}

public readonly struct FlowBinding
{
    public FlowOutPort     Source      { get; }
    public FlowInPort      Destination { get; }
    public FlowConnection  Connection  { get; }
    public FlowBinding(FlowOutPort src, FlowInPort dst, FlowConnection conn)
    {
        Source = src; Destination = dst; Connection = conn;
    }
}
```

`GraphTopology.Bake` produces these by iterating the asset's
`connections` (data) and `flowEdges` (flow). For each data edge it looks
up the typed `InputPort<T>` / `OutputPort<T>` pair via reflection on
field type once at bake (or via the existing `PortMeta` catalog if
that's cheaper) and emits `new DataBinding(() => input.Connect(output))`.
The `Action` allocation is paid once per edge at bake; runtime calls
are pure delegate-invokes with no further reflection.

### `EntryRuntimeNode<TPayload>`

```csharp
public abstract class EntryRuntimeNodeBase : RuntimeNode
{
    public abstract Type PayloadType { get; }

    // R3 — concrete on the base. Scans Ports for the sole FlowOutPort and
    // returns it. Throws on zero or more than one, which forces multi-out
    // entries (CardEntry: Validate + Execute) into RunFromFlowOut. Cached
    // on first call so repeated dispatches are O(1).
    FlowOutPort? _defaultOut;
    bool _defaultOutResolved;

    public FlowOutPort GetDefaultOut()
    {
        if (_defaultOutResolved) return _defaultOut
            ?? throw new InvalidOperationException(
                $"Entry {GetType().Name} has no FlowOutPort.");

        FlowOutPort? found = null;
        int count = 0;
        foreach (var p in Ports.Values)
        {
            if (p is not FlowOutPort fo) continue;
            count++;
            found = fo;
        }
        _defaultOutResolved = true;
        _defaultOut         = count == 1 ? found : null;

        if (count == 0)
            throw new InvalidOperationException(
                $"Entry {GetType().Name} has no FlowOutPort — cannot dispatch via Run<TEntry>.");
        if (count > 1)
            throw new InvalidOperationException(
                $"Entry {GetType().Name} has {count} FlowOutPorts — use RunFromFlowOut to pick one.");
        return _defaultOut!;
    }
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
- `GetDefaultOut()` for `Run<TEntry>` single-out dispatch.
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
        //    instantiated RuntimeNodes from deserialization — Bake
        //    does NOT clone, R12). All runners built from this asset
        //    share these instances.
        // 2. Bucket asset.connections (data) and asset.flowEdges
        //    (flow) by destination node-id.
        // 3. For each data edge, look up src OutputPort and dst
        //    InputPort by name from each node's `Ports` dict, then
        //    emit `new DataBinding(() => dst.ConnectFrom(src))` (R16).
        //    No reflection — `ConnectFrom` is a non-generic virtual
        //    on Port, overridden once by InputPort<T> to do the typed
        //    cast and assignment.
        // 4. Create FlowConnection instances exactly once each.
        // 5. For each node: hand it its slice via node.Build(slice).
        // 6. Build entriesByPayload dict.
        // 7. Return BakedGraph(nodes, entriesByPayload).
    }
}
```

### `GraphRunner`

```csharp
public abstract class GraphRunner
{
    protected internal IReadOnlyList<RuntimeNode>                      Nodes            { get; }
    protected internal IReadOnlyDictionary<Type, EntryRuntimeNodeBase> EntriesByPayload { get; }

    protected GraphRunner(BakedGraph baked)
    {
        Nodes            = baked.Nodes;
        EntriesByPayload = baked.EntriesByPayload;
    }

    public virtual void Initialize() { }   // consumer hook, default no-op

    // Default-flow-out dispatch by payload type. Throws if the entry has
    // zero or multiple FlowOutPorts (R3) — multi-out shapes use the
    // RunFromFlowOut path below.
    public Task<Flow> Run<TEntry>(TEntry payload, CancellationToken ct = default)
        where TEntry : class
    {
        if (!EntriesByPayload.TryGetValue(typeof(TEntry), out var entry))
            throw new InvalidOperationException(
                $"No entry for {typeof(TEntry).FullName}.");

        var flow = NewFlow(payload, ct);
        return RunFromEntry(entry, flow);
    }

    // Explicit-flow-out dispatch (multi-out entry shapes).
    protected async Task<TResult?> RunFromFlowOut<TResult>(
        object payload, FlowOutPort flowOut, CancellationToken ct = default)
    {
        var dest = flowOut.Connection?.Destination;
        if (dest == null) return default;

        var flow = NewFlow(payload, ct);
        await RunFromInPort(dest, flow);
        return flow.ReadResult<TResult>();
    }

    // R7 — no pool. New Flow per dispatch. The Flow ctor is internal so
    // callers can't bypass GraphRunner's wiring of payload / runner / token.
    Flow NewFlow(object payload, CancellationToken ct) =>
        new Flow(payload, this, ct);

    async Task<Flow> RunFromEntry(EntryRuntimeNodeBase entry, Flow flow)
    {
        var defaultOut = entry.GetDefaultOut();
        var dest = defaultOut.Connection?.Destination;
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
        // Clears every per-port flow-keyed cache touched during this run.
        // No pool to return to — the Flow drops out of scope after this
        // method returns and is GC'd along with its dictionaries.
        flow.InvalidateAll();
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
        // Positional ctor (R6) — Owner / Name are immutable.
        Validate = new FlowOutPort(this, nameof(Validate));
        Execute  = new FlowOutPort(this, nameof(Execute));
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
        In = FlowInPort.Sync(this, nameof(In),
            flow => Condition.Read(flow) ? True : False);
}
```

### `Return<TResult>` — typed terminator

```csharp
[Serializable]
[GraphNode(Category = "Flow")]
public sealed partial class Return<TResult> : RuntimeNode
{
    public FlowInPort         In    = null!;
    public InputPort<TResult> Value = null!;

    partial void InitializePorts() =>
        In = FlowInPort.Sync(this, nameof(In),
            flow => flow.Return(Value.Read(flow)));
}
```

### `Return` — void terminator (R13)

Distinct from `Cancel` — signals successful done with no return value.

```csharp
[Serializable]
[GraphNode(Category = "Flow")]
public sealed partial class Return : RuntimeNode
{
    public FlowInPort In = null!;

    partial void InitializePorts() =>
        In = FlowInPort.Sync(this, nameof(In), flow => flow.Return());
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
        In = FlowInPort.Sync(this, nameof(In), flow => flow.Cancel());
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
        In = FlowInPort.Async(this, nameof(In), async flow =>
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

        Begin = FlowInPort.Sync(this, nameof(Begin), flow =>
        {
            flow.SetSlot(this, 0);
            return Count.Read(flow) > 0 ? Body : Done;
        });

        Continue = FlowInPort.Sync(this, nameof(Continue), flow =>
        {
            flow.InvalidateAll();                       // wipe iteration K's caches
            var i = flow.GetSlot<int>(this) + 1;
            flow.SetSlot(this, i);
            return i < Count.Read(flow) ? Body : Done;
        });
    }
}
```

### `OnTrigger<TEvent>` — Mode-3 event entry (R2)

`OnTrigger<TEvent>` is a special entry: it's both the runtime-node type
AND its own payload type (the host wraps an inner event in an
`OnTrigger<TEvent>` wrapper instance and dispatches that). Today's
mutable `Event` field is replaced by:

- An **inner-event field** on the payload-wrapper instance (`Inner`) —
  set by the host before dispatch. This is per-dispatch instance state,
  not per-runner shared state, so it doesn't violate D5.
- An **`Event` output port** on the runtime node — reads the wrapper's
  `Inner` field via the flow's payload.

```csharp
[Serializable]
public sealed class OnTrigger<TEvent>
    : EntryRuntimeNode<OnTrigger<TEvent>>, IOnTrigger
    where TEvent : class
{
    // Editor-time config — Timing is serialized on the node itself, not
    // the payload-wrapper. Static config, no per-run state.
    public Timing Timing { get; set; }

    // Inner event payload. Host code sets this on the *wrapper instance*
    // before dispatch:
    //   var wrapper = new OnTrigger<DamageDealt> { Inner = evt };
    //   await runner.Run(wrapper);
    // Renamed from the v1 `Event` field to free that name for the port.
    public TEvent? Inner;

    public FlowOutPort        FlowOut;
    public OutputPort<TEvent> Event;

    public OnTrigger()
    {
        FlowOut = new FlowOutPort(this, nameof(FlowOut));
        Event   = new OutputPort<TEvent>(
            flow => flow.GetPayload<OnTrigger<TEvent>>()!.Inner!);
        Ports.Add(FlowOut.Name, FlowOut);
        Ports.Add(nameof(Event), Event);
    }
}
```

`FlowOut` is the only `FlowOutPort` on the node, so
`EntryRuntimeNodeBase.GetDefaultOut()` returns it for `runner.Run<OnTrigger<TEvent>>`
dispatch. The `Inner` rename is a breaking change for any host code
that read `OnTrigger<TEvent>.Event` directly — those call sites move to
`wrapper.Inner` for writes; reads inside the graph go through the
`Event` port.

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

### Resolved

- **Q-pool** — resolved (R7). No pool; `new Flow(...)` per dispatch.
  Pool reintroduction stays a future optimisation.
- **Q-async-overload** — resolved (R9). Named static factories
  `FlowInPort.Sync` / `FlowInPort.Async` with `Task<FlowOutPort>`.
- **Q-default-out** — resolved (R3). `EntryRuntimeNodeBase.GetDefaultOut()`
  scans `Ports` for the sole `FlowOutPort` and throws on zero or
  multiple.

---

## Migration / cutover

Single hard-cutover commit ("refactor(GraphFlow): per-In handlers,
Bake builder, Flow as per-run context"). Old `GraphController` /
`GraphExecutor` deleted in the same commit that introduces the
new shape. No alive-but-obsolete transition window.

Order of work within the commit:

1. **Port API rewrite.**
   - `Port` base gains `internal virtual void ClearCache(Flow) {}` (R8)
     and `internal virtual void ConnectFrom(Port) { throw … }` (R16).
   - `OutputPort<T>` — `Func<Flow, T>` compute, flow-keyed cache,
     `cache:false` opt-out, `ClearCache` override.
   - `InputPort<T>` — `_source: OutputPort<T>?`, `IsConnected`,
     `Connect(OutputPort<T>)`, `Read(Flow)`, `ConnectFrom(Port)` override
     for the bake-side seam (R16). Old `_fallback` and `AcceptOutput`
     deleted.
   - `FlowOutPort` — `.End` sentinel; positional ctor `(owner, name)` (R6).
   - `FlowInPort` — no public ctor; `FlowInPort.Sync(owner, name, Func<Flow, FlowOutPort>)`
     and `FlowInPort.Async(owner, name, Func<Flow, Task<FlowOutPort>>)`
     factories (R9). Convention: name is `"In"` not `"FlowIn"` (R14).
   - **Delete** abstract `Connection` and generic `Connection<T>` (R1).
     Slim `Connection.cs` to just `FlowConnection`.

2. **Rewrite `Flow`.**
   - Internal ctor `(payload, runner, token)` — immutable for the
     run's lifetime.
   - `Outcome { Running, Returned, Cancelled }`; default Running.
   - Payload via `GetPayload<T>()` only (no setter).
   - `Return<T>(value)` / `Cancel()` set Outcome and return
     `FlowOutPort.End`.
   - `RegisterTouched(Port)` / `InvalidateAll()` (calls `port.ClearCache(this)`
     directly — R8).
   - `GetSlot<T>(owner)` / `SetSlot<T>(owner, value)`.
   - **Delete** `GoTo`, `Stop`, `ConsumeNext`, `Scope`, `Reason`,
     `ResetForReuse`, `_nextPort` (R7).

3. **Add non-generic `GraphAsset` base** (R10), abstract (R11), holding
   `nodes` / `connections` / `flowEdges`. `GraphAsset<TRunner>`
   becomes a typed marker subclass.

4. **Add `NodeBuildSlice`, `DataBinding`, `FlowBinding` structs** (R4).
   `DataBinding.Apply` closes over the typed pair via the non-generic
   `Port.ConnectFrom(Port)` seam (R16) — one virtual call per data
   edge, no reflection.

5. **Add `BakedGraph` + `GraphTopology.Bake(GraphAsset)`.** Pure
   topology resolution over the asset's existing deserialized
   `RuntimeNode` instances (R12 — Bake does NOT clone nodes; runners
   built from the same asset share node instances per D5). Produces
   per-node slices and the `EntriesByPayload` dict.

6. **Add `GraphBuilder<TRunner>`.** Cache + Build template method
   + abstract CreateRunner.

7. **Rewrite `GraphRunner`.**
   - Forced base ctor `(BakedGraph)`.
   - `Run<TEntry>` — `entry.GetDefaultOut()` dispatch; throws on
     multi-out entries (R3).
   - `RunFromFlowOut<TResult>` — explicit-out dispatch.
   - Walk loop awaits `current.Invoke(flow)`, checks
     `FlowOutPort.End` sentinel, calls `flow.InvalidateAll()` on exit
     (no pool return — R7).
   - `Initialize` virtual no-op default.
   - `NewFlow(payload, ct)` helper just `new Flow(...)` (R7).

8. **Rewrite `RuntimeNode`.**
   - `Build(in NodeBuildSlice)` internal virtual default that
     applies the slice (R4).
   - `Initialize(GraphRunner)` public virtual no-op default.
   - **Delete** `Execute`, `Bind(...)` (R5).

9. **Rewrite `RuntimeNode<TRunner>`.**
   - Static `Runner(Flow)` helper.
   - Sealed Initialize override forwarding to typed.
   - Drop `_runner` field, drop typed Execute.

10. **Rewrite `EntryRuntimeNodeBase` and `EntryRuntimeNode<TPayload>`.**
    - Base: `PayloadType` + concrete `GetDefaultOut()` (R3).
    - Typed: adds `OutputPort<TPayload> Payload`. Nothing else.
    - **Delete** `_runFromHere`, `BindRunner`, `Run(TEntry)`,
      `Run(object)`, `BindForRun<TRunner>`, `Payload` field,
      `SetPayload`.

11. **Rewrite `OnTrigger<TEvent>`** (R2).
    - Rename `Event` field → `Inner` (the inner-event payload field).
    - Add `OutputPort<TEvent> Event` reading
      `flow.GetPayload<OnTrigger<TEvent>>()!.Inner!`.
    - Remove `Execute` override.
    - `Timing` property unchanged.

12. **Rewrite all built-in nodes** (Add, And, Or, Not, Branch,
    GreaterThan, LessThan, Subtract, Multiply, IntToString,
    `Return<TResult>`, void `Return` (R13), Cancel, Wait, Loop) to the
    new shape — pure nodes have no flow ports + lazy `OutputPort<T>`;
    imperative nodes use `FlowInPort.Sync` / `FlowInPort.Async`
    factories (R9). All flow-in fields are named `In` and registered
    via `nameof(In)` (R14). `Wait` and `Loop` are NEW — they ship in
    this commit (R17).

13. **Generator changes.**
    - **Per-payload entry runtime emission** (R15). Each payload `T`
      that implements `IGraphEntry` produces one `TRuntime` class
      deriving from the **package's hand-written entry base** (e.g.
      `OnPlayRuntime : CardEntry<OnPlay>`). The base provides the flow
      outs (`Validate`, `Execute`, etc.) once per package; the
      generator emits **only** per-field `OutputPort<T>` data ports
      closing over `flow.GetPayload<T>()!.FieldX`. No backing
      `_fooValue` fields, no `Execute` override, no `FlowOut`
      emission — the base class's flow outs cover that.
    - For packages that don't define a per-package entry base, the
      generator falls back to deriving from `EntryRuntimeNode<T>` and
      emitting a single `FlowOut` positionally `new FlowOutPort(this,
      nameof(FlowOut))` (R6). Single-out shape; multi-out requires the
      hand-written base.
    - Per-`[GraphNode]` partial ctor: emit FlowInPort fields via
      `FlowInPort.Sync(this, nameof(In), …)` from `InitializePorts`
      (R14) — framework no longer constructs FlowInPorts in the
      partial itself, since the handler is required and only the user
      knows it. The partial keeps creating data ports and FlowOutPorts.
    - Catalog emit unchanged for now (Modify/Decompose/Construct
      deferred).

14. **Delete** `GraphController.cs`, `GraphExecutor.cs`. Migrate
    all sample + test call sites to `builder.Build(asset)` /
    `runner.Run(payload)`.

15. **Run** snapshot tests + smoke tests. Refresh snapshots if
    generator output shifts.

Estimated net delete: ~250–350 lines of framework (controller +
executor + Connection<T> + EntryRuntimeNode complexity + per-node
Execute boilerplate). Add: ~300 lines of framework (BakedGraph +
GraphBuilder + GraphTopology + NodeBuildSlice + per-In handler
infrastructure + GetDefaultOut). Roughly net-zero on framework
size; significantly different shape. Plus generator changes for
Payload-from-Flow capture, FlowInPort factory emission, and the
OnTrigger reshape.

---

## Test impact

- **Snapshot tests** — `HarnessGraphRegistry.g.cs` registry shape
  changes: `BindForRun` plumbing is removed and entry runtime
  closure capture switches to `flow.GetPayload<T>()!.FieldX`. The
  per-payload entry runtime no longer holds backing `_fooValue`
  fields or an `Execute` override. Snapshots regenerated post-change.
- **`Tests/RuntimeSmokeTests`** — fixtures use `controller.Initialize`
  + `controller.Run`. Migrate to `new SmokeBuilder(...).Build(asset)`
  + `runner.Run`. Hand-authored fixtures that called
  `node.Bind(...)` (R5) move to `inputPort.Connect(outputPort)` /
  direct `FlowConnection` assignment.
- **`CardSandbox/Tests/Strike500Tests`** — uses `trig.Run(payload)`
  for trigger pattern-match. Migrate to per-runner subscription
  pattern with `runner.Run(payload)`. Any code that read
  `OnTrigger<TEvent>.Event` directly moves to either:
  - host writes: `wrapper.Inner = evt` before dispatch (R2);
  - graph reads: wire the `Event` output port to a downstream
    `InputPort<TEvent>`.
- **Editor / generator** — `RegisterAdditional` partial on
  `<Stem>GraphRegistry` is unchanged. Per-package OnTrigger /
  Return shims need their entry-runtime base updated (still use
  the existing catalog, but the new EntryRuntimeNode<T> shape).
- **`PortValueResolver`** — unchanged (bake-time, doesn't touch
  controller/executor).
- **NEW: Loop node test** — concurrent runs, cache invalidation,
  iteration counter on flow slot. Validates the multi-In shape
  end-to-end.
- **NEW: GetDefaultOut throw test** — multi-out entry (CardEntry
  shape) dispatched through `Run<TEntry>` should throw the
  expected message; `RunFromFlowOut` should succeed.

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
