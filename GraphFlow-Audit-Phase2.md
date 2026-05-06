# GraphFlow Phase-2 Audit

Audit of `Assets/Packages/com.scaffold.graphflow/Runtime/` after the Phase-1 cleanup.
Prior audit (`GraphFlow-Audit.md`) items C1–C3, S1–S7, G1–G6 are all resolved
(no remaining references to `GraphController`, `EntryBridge`, `IEffectScope`,
`GraphExecutor`, `LifecycleInterfaces`, `CrossAsmSpikeNode`).

Runtime is **675 LOC across 19 files**. Tight.

## Verdict

| Question | Answer |
|---|---|
| Code is good | Mostly yes — small surface, clear shapes, no dead types |
| Hotpath clean | Mostly — three avoidable allocations per step, one concurrency hazard |
| Runtime reflection | **Zero.** No `System.Reflection`, no `dynamic`, no LINQ in `Runtime/` |
| Clear code smells | A handful; none architectural |

---

## Hotpath findings

### H1. `Task.FromResult` allocation per sync node fire — `Ports/FlowInPort.cs:23`

```csharp
public static FlowInPort Sync(
    RuntimeNode owner, string name, Func<Flow, FlowOutPort> handler) =>
    new(owner, name, flow => Task.FromResult(handler(flow)));
```

Every step in `GraphRunner.RunFromInPort` does
`await current.Invoke(flow).ConfigureAwait(false)`. For sync nodes (Branch,
Cancel, Loop, Return, Add, Not, And, Or…) every fire allocates a fresh
`Task<FlowOutPort>` via `Task.FromResult`. On a 50-node sync graph that's
~50 task allocations per `Run`.

**Fix:** Switch `FlowInPort.Invoke` to `Func<Flow, ValueTask<FlowOutPort>>`.
`ValueTask.FromResult` (or implicit conversion of `FlowOutPort`) is a struct.
Async nodes still wrap `Task` via `new ValueTask<>(task)`. Runner stays
identical.

---

### H2. `OutputPort<T>._cache` is a per-port `Dictionary<Flow, T>` — `Ports/OutputPort.cs:10`

```csharp
readonly Dictionary<Flow, T> _cache = new();
```

Two issues:

1. Every output port allocates a dictionary at hydration, even if never
   read. A 1k-node graph with avg 3 output ports = 3k dictionaries baked in
   for the runner's lifetime.
2. The dictionary holds at most **one** live entry at a time (per concurrent
   run). It's a 1-slot cache wrapped in a hash table.

**Fix:** Move the cache onto `Flow` itself. `Flow` already has
`Dictionary<object, object> _slots`; reuse that or add a parallel dict
keyed by output port. One dictionary per run instead of one per port. Bonus:
fixes H3 and CC1 below.

---

### H3. `OutputPort._cache` allocates even when `cache: false` — `Ports/OutputPort.cs:10`

```csharp
public OutputPort(Func<Flow, T> compute, bool cache = true)
{
    _compute = compute;
    _shouldCache = cache;
}
```

`Loop.Iteration` passes `cache: false`. The dictionary is still allocated
and never used. Subsumed by H2's fix.

---

### H4. `Flow.GetSlot<T>` boxes value-type slots — `Flow/Flow.cs:62-66`

```csharp
Dictionary<object, object>? _slots;

public T GetSlot<T>(object owner) =>
    _slots != null && _slots.TryGetValue(owner, out var v) ? (T)v : default!;

public void SetSlot<T>(object owner, T value) =>
    (_slots ??= new())[owner] = value!;
```

`Loop.Continue` does `flow.GetSlot<int>(this)` and `flow.SetSlot(this, i)`
every iteration. `int → object` boxes on set, unboxes on get, plus the dict
lookup. Per loop iteration, per Loop node.

**Fix (low-priority):** Either accept the cost (Loop is the only consumer
right now) or specialize a typed slot: `Flow.GetSlot<TKey, TValue>` with a
typed dictionary lazily allocated per `TValue`. Not worth the complexity
unless a hot loop appears in profiling.

---

## Concurrency

### CC1. `OutputPort._cache` is **shared mutable state** across concurrent flows

The `GraphFlow-Audit.md` G+4 invariant says "two concurrent runs = two flows,
no shared state." That holds for `Flow` itself — but `OutputPort._cache`
lives on the port, which is owned by the runner and shared across all flows
that the runner dispatches. `Dictionary<,>` is not thread-safe; two flows
hitting the same `Read(flow)` race on the underlying buckets.

This isn't a hypothetical: anyone calling `runner.Run(...)` twice without
awaiting the first violates the invariant silently.

**Fix:** Same as H2 — move the cache onto `Flow`. Each run owns its own
cache. The "no shared state" invariant becomes structural rather than
documented.

If concurrent runs aren't supposed to be supported, document that on
`GraphRunner.Run` and add a debug-build assert (single-active-flow guard).

---

## Code smells

### SM1. `OnTrigger<TEvent>` uses node-as-payload — `Nodes/OnTrigger.cs`

```csharp
public sealed class OnTrigger<TEvent> : EntryRuntimeNode<OnTrigger<TEvent>>
{
    public TEvent? Inner;
    ...
    Event = new OutputPort<TEvent>(
        flow => flow.GetPayload<OnTrigger<TEvent>>()!.Inner!);
}
```

`Run<OnTrigger<TEvent>>` requires the caller to construct an
`OnTrigger<TEvent>` instance with `Inner` mutated, hand it back to the
runner, which then asks the flow for *the node it dispatched on* to read
`Inner` off it. The node is its own payload. Two `!`s in the path.

**Fix:** Make payload `TEvent` directly. `EntryRuntimeNode<TEvent>` with
`Event = new OutputPort<TEvent>(flow => flow.GetPayload<TEvent>()!)`. Drop
`Inner`. Drop the `IOnTrigger` interface (it only carries `Timing`, which
the editor reads — that can move to a `[GraphNode]` config or a separate
serialized field).

If the indirection is genuinely needed for the editor catalog (closed
generic over `OnTrigger<TEvent>` for picker resolution), leave a one-line
comment that says so.

---

### SM2. `FlowOutPort.End` sentinel with `null!` Owner — `Ports/FlowOutPort.cs:7`

```csharp
public static readonly FlowOutPort End = new(null!, "<end>");
```

Comparison via `ReferenceEquals(next, FlowOutPort.End)` works, but
`End.Owner` will NRE if anyone ever reads it. Footgun.

**Fix:** Either make `Owner` nullable on `FlowOutPort` (it's only read by
diagnostics, which can handle null), or model termination as a separate
state — e.g., `FlowInPort.Invoke` returns `FlowOutPort?` where `null` means
end. The current sentinel pattern compiled to NRT is doing exactly what NRT
was designed to prevent.

---

### SM3. `EntryRuntimeNodeBase.GetDefaultOut` lazy-scans `Ports.Values` — `Nodes/EntryRuntimeNode.cs:14-37`

The first `Run<TEntry>` after each cold load walks the entry's port dict to
find the single `FlowOutPort`. Cached after that. Build-time information
discovered at first-run-time.

**Fix:** Resolve in `RuntimeNode.Build` (or `EntryRuntimeNodeBase.Build`)
once during topology bake. Drop `_defaultOutResolved`. Field becomes
`readonly` after construction. Saves the lazy branch on the entry path.

---

### SM4. `[Serializable]` on `PortMeta` despite carrying `Type` — `Markers/PortMeta.cs:11`

`Type` is not Unity-serializable. The attribute is misleading; the catalog
holds `PortMeta` in memory only.

**Fix:** Drop `[Serializable]`.

---

### SM5. `Port.ConnectFrom` base throws — `Ports/Port.cs:10-12`

```csharp
internal virtual void ConnectFrom(Port output) =>
    throw new InvalidOperationException(...);
```

Only `InputPort<T>` legitimately implements this. Output/Flow ports
inheriting from `Port` will throw. The base could be `abstract` (forcing
each port to declare intent) or `ConnectFrom` could move off `Port` entirely
and become a virtual on `InputPort` only — `GraphTopology` already checks
the destination is an `InputPort<T>` via the `dst.ConnectFrom(src)` site.

**Fix (small):** Either make it `abstract` on `Port`, or hoist it to a
narrower interface so the impossible-to-call branches go away. Trade-off:
the base method is the only thing keeping `DataBinding(() =>
dst.ConnectFrom(src))` polymorphic. Acceptable as-is — flagging because
it's a contract that bakes in throwing on unreachable paths.

---

### SM6. `GraphBuilder._cache` is unbounded — `Builder/GraphBuilder.cs:8`

```csharp
readonly Dictionary<GraphAsset, BakedGraph> _cache = new();
```

If a single `GraphBuilder` instance is used across many `GraphAsset` loads
and the user never unloads the builder, baked graphs accumulate forever.
Each `BakedGraph` retains every node and the entries dictionary.

**Fix:** Document the lifetime contract on `GraphBuilder` ("one per asset
or one per scene; not a singleton"), or add a `ConditionalWeakTable<>` so
the cache disappears with the asset. Likely a non-issue in practice since
builders are short-lived per scene; flag-only.

---

## What's still good — keep enforcing

Reaffirming the original `GraphFlow-Audit.md` G+1..G+15 invariants — all
still hold:

- **No reflection in runtime.** Zero `System.Reflection` usage.
  Generator-emitted code is the only place type info is encoded; runtime
  consumes it as direct calls and dictionary lookups.
- **Hydration-once.** `GraphTopology.Bake` resolves every wire upfront.
  `GraphRunner.RunFromInPort` is a 7-line `while` loop with no metadata
  lookups.
- **Single cast seam.** `InputPort<T>.ConnectFrom` does the only type
  collapse. Everything else is statically typed.
- **`Flow` is per-run.** Per-run state is on `Flow`. (Modulo CC1 — fix
  there is local.)
- **Built-ins are minimal.** `Add`/`And`/`Or`/`Not`/`Branch`/`Cancel`/`Return`
  are 1–4 lines of body each.

---

## Priority

| Priority | Item | Win |
|---|---|---|
| P0 | **CC1 + H2** (move cache onto Flow) | Fixes concurrency hazard *and* removes per-port dict alloc *and* removes H3 |
| P1 | **H1** (`ValueTask`-ify `FlowInPort.Invoke`) | One Task allocation removed per sync step |
| P2 | **SM3** (resolve default flow-out at bake) | Cleaner semantics, removes lazy state |
| P2 | **SM1** (drop node-as-payload in `OnTrigger`) | Removes two `!`s and a confusing pattern |
| P3 | SM2, SM4, SM5, SM6 | Cosmetic / documentation |
| P3 | H4 (typed slots) | Defer until profiling shows it |

P0 is one change that resolves three findings — start there.
