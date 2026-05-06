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

## Node-state audit

**Rule (refined):** nodes are baked once and shared across every `Run`
against a runner. Same applies to ports (owned by nodes) and to runners.
Per-run *mutable* state belongs on `Flow` only.

**Bake-time config is allowed.** A field that is set during construction or
`Build()` and never written at runtime is configuration, not state — it
doesn't fight across runs because nothing is writing it. Useful when the
editor/catalog needs to read static metadata off a baked node (e.g.
`OnTrigger.Timing`).

The discipline is therefore: anything held on a node, port, or runner must
be *unwritable* after `Build()`. Either no public setter, or `init`-only,
or readonly-after-ctor — the language enforces the contract.

Walked every `RuntimeNode` subclass under `Runtime/`, `Samples/`,
`Samples~/`, and `Tests/`. Five violations + one to harden.

### NS1. `EntryRuntimeNodeBase._defaultOut` / `_defaultOutResolved` — `Nodes/EntryRuntimeNode.cs:11-12`

Lazy-cached on first `Run`. Idempotent (same value every time), so no
observable bug today, but still a write to shared fields *after* `Build`.
Resolve at Build time and the fields become bake-time config (allowed).
Same fix as SM3.

### NS2. `OnTrigger<TEvent>.Inner` — `Nodes/OnTrigger.cs:12`

```csharp
public TEvent? Inner;
```

Public mutable field on a `[Serializable]` node that is shared across runs.
Unlike `Timing`, `Inner` is *per-run data* (the dispatched event), not
config. The current runtime path reads `Inner` off the **payload** instance
(`flow.GetPayload<OnTrigger<TEvent>>()!.Inner!`), not off the baked node, so
there is no live race today. But the type's shape — same class for baked
node and dispatch payload — makes the violation latent: anyone who writes
`bakedNode.Inner = x` (or assumes the baked node carries the event) breaks
concurrent runs silently.

### NS3 (downgrade — harden, don't remove). `OnTrigger<TEvent>.Timing` — `Nodes/OnTrigger.cs:10`

```csharp
public Timing Timing { get; set; }
```

`Timing` **is** valid config-on-node — it's set at bake (object initializer
or editor wiring), shared across runs without contention because nothing
writes it at runtime, and the editor/catalog reads it for metadata. This
is exactly the shape the rule allows.

The only smell is that the contract isn't enforced by the type. Today
nothing stops a runtime caller from writing `bakedNode.Timing = X` and
breaking concurrent runs. Make it unwritable after construction:

```csharp
public Timing Timing { get; init; }
```

Update `IOnTrigger`:

```csharp
public interface IOnTrigger { Timing Timing { get; } }
```

Object-initializer syntax (`new OnTrigger<DamageDealt> { Timing = Timing.Before }`)
still compiles; runtime mutation becomes a compile error. Unity's
SerializeReference path uses field-level reflection (not the property
setter), so deserialization is unaffected.

This is the template for any future config-on-node: `init`-only or
`readonly` after ctor. If a new field can't satisfy that, it isn't config
— it's state, and it belongs on `Flow`.

### NS4. `OutputPort<T>._cache` — `Ports/OutputPort.cs:10`

Cache lives on the port (owned by the node) and is mutated per `Read`.
Concurrent runs on the same baked graph race the dictionary. Same finding
as CC1 / H2 below — listed here too because it *is* node-state, just
delegated to the port.

### NS5. `MySmokeRunner.LastLogMessage` — `Samples~/M0Sandbox/.../MySmokeRunner.cs:7`

```csharp
public string LastLogMessage { get; private set; } = "";
public void RecordLog(string message) => LastLogMessage = message;
```

Per-run mutable state on a shared runner. Two concurrent runs would clobber
each other's last-log. Sample code gets copy-pasted into real packages —
this teaches the wrong shape.

**Fix:** inject a sink (e.g. `IGraphLogSink`) and write to that. The runner
holds *services*, not *state*.

### NS6. `TestRunner.LastLogMessage` — `Tests/Fixtures.cs:9-10`

Same pattern as NS5 in the test fixture. Tests run sequentially so it
"works," but the fixture is the canonical "how to author a runner" example
in the test pack — it propagates the bad shape.

**Fix:** mirror NS5's resolution in the fixture.

### Clean nodes (for the record)

All built-ins (`Branch`, `Cancel`, `Loop`, `Wait`, `Add`, `Multiply`,
`Subtract`, `And`, `Or`, `Not`, `GreaterThan`, `LessThan`, `IntToString`,
`Return`, `Return<T>`) are stateless — port handles only. `Loop` uses
`flow.SetSlot(this, …)` for its iteration counter, which is the correct
pattern.

Sample nodes (`Strike500Dispatcher`, `PlusOneDamageMutator`,
`CardCommandDispatcher<,>`, `IntToStringRuntime`, `MyDispatcherBase<,>`)
are stateless. Test nodes (`TestEntryRuntime`, `TestIntToStringRuntime`,
`TestLogDispatcherRuntime`, `TestEchoDispatcherRuntime`) are stateless.

`CardEffectRunner` and `SpikeRunner` carry only readonly service refs —
clean.

---

## Concurrency

**Decision: concurrent `Run` against the same runner is supported.**
The G+4 invariant ("two concurrent runs = two flows, no shared state, async
safety without locks") is normative — the implementation must enforce it.

### CC1. `OutputPort._cache` is **shared mutable state** across concurrent flows

The current port-owned `Dictionary<Flow, T>` was chosen for two reasons:

1. **Typed storage** — value-type outputs (`int`, `bool`, `float`) cache
   without boxing.
2. **Per-port invalidation locality** — `Flow._touched` collects ports
   that hit the cache, and `Port.ClearCache(flow)` evicts at end-of-run on
   the port itself.

Both are real wins, but `Dictionary<,>` is not thread-safe; two flows
reading the same `OutputPort<T>` on the same baked graph race the buckets.
That violates the supported concurrency model.

**Fix:** move the cache to `Flow`. Each run owns its own cache → no shared
mutable state → invariant becomes structural.

```csharp
// Runtime/Ports/OutputPort.cs
public sealed class OutputPort<T> : Port
{
    readonly Func<Flow, T> _compute;
    readonly bool _shouldCache;

    public OutputPort(Func<Flow, T> compute, bool cache = true)
    {
        _compute = compute;
        _shouldCache = cache;
    }

    public T Read(Flow flow) =>
        _shouldCache ? flow.ReadCached(this, _compute) : _compute(flow);
}

// Runtime/Flow/Flow.cs
Dictionary<Port, object?>? _cache;

internal T ReadCached<T>(Port port, Func<Flow, T> compute)
{
    _cache ??= new();
    if (_cache.TryGetValue(port, out var v)) return (T)v!;
    var fresh = compute(this);
    _cache[port] = fresh;
    return fresh;
}

public void InvalidateAll() => _cache?.Clear();
```

Drop `Flow._touched`, `Flow.RegisterTouched`, `Port.ClearCache`,
`OutputPort.ClearCache`. The `Port` base shrinks to just `ConnectFrom`.

**Cost we accept:** value-type cached results box on cache miss (one alloc
per miss), unbox on hit (no alloc). For an `OutputPort<int>` that's read N
times in a run, that's one alloc instead of zero. Bounded; acceptable
price for the concurrency invariant.

**Boxing mitigation if it ever shows up in profiling:** mark cheap pure
outputs (`Add`, `Subtract`, `GreaterThan`…) with `cache: false`. The
recompute is just two upstream reads (themselves cached) plus an op —
cheaper than the dict round-trip plus boxing. Today these default to
`cache: true`; flipping the default for pure-arithmetic built-ins would
remove the boxing surface for the common cases. Defer until profiling
warrants.

This change kills NS4, CC1, H2, and H3 in one move.

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
| P0 | **CC1 + H2 + NS4** (move cache onto Flow) | Fixes concurrency hazard *and* removes per-port dict alloc *and* removes H3 *and* clears the OutputPort node-state violation |
| P1 | **H1** (`ValueTask`-ify `FlowInPort.Invoke`) | One Task allocation removed per sync step |
| P1 | **NS5 + NS6** (drop `LastLogMessage` from sample/test runners) | Removes the wrong-shape exemplar before it gets copy-pasted |
| P2 | **SM3 / NS1** (resolve default flow-out at bake) | Cleaner semantics, removes lazy state |
| P2 | **SM1 / NS2** (split `OnTrigger` node from payload — drop `Inner` from baked node) | Removes node-state surface and the two `!`s |
| P2 | **NS3** (`Timing` becomes `init`-only) | Enforces config-vs-state at the type level; one-line change |
| P3 | SM2, SM4, SM5, SM6 | Cosmetic / documentation |
| P3 | H4 (typed slots) | Defer until profiling shows it |

P0 is one change that resolves three findings — start there.
