# Port Cache Migration — Proposal

Status: **proposal, not yet a decision.** Backed by a sandbox spike at
`Assets/Sandbox/PortCacheSpike/` (10 tests, including correctness for
flow-index reuse + bench data). Replaces the dictionary-based per-flow
cache in `Flow` with per-port typed `Entry[]` indexed by `flow.Index`,
invalidated by a global monotonic version counter.

## Context

The Properties spike at `Assets/Sandbox/PropertiesSpike/` answered the
question "should `com.unity.properties` replace the source generator
on the runtime hot path?" — clearly **no**. (Findings: source-gen wins;
DOTS itself reserves Properties for managed-component reflection and
editor tooling, never `IJobChunk`.) That spike incidentally surfaced a
larger, generator-independent finding: **the GraphFlow port-read
hot path is dominated by `Flow._cache`, not by anything Properties
could fix.**

Specifically, `Assign_GraphPorts` (current `InputPort.Read` →
`OutputPort.Read` → `Flow.ReadCached`) measured **466 ns/op with 2
allocs/op** vs hand-rolled 13 ns/op — a **35× gap with non-zero GC**.
The allocs come from boxing value types into `Dictionary<Port, object?>`;
the time comes from dictionary lookup + virtual dispatch.

The source generator can't fix this — it's an architecture issue in the
runtime, independent of how ports get wired.

## Proposal

Replace `Flow._cache : Dictionary<Port, object?>` with **per-port typed
`Entry[]` arrays keyed by `flow.Index`**, invalidated via a **global
monotonic `int` version counter** carried on each flow.

### Shape

```csharp
public sealed class Flow
{
    static int s_globalVersion;

    public int Index { get; }                  // assigned from runner pool
    public int CacheVersion { get; private set; }

    internal Flow(...)
    {
        // ... existing fields
        Index  = runner.AcquireFlowIndex();
        CacheVersion = Interlocked.Increment(ref s_globalVersion);
    }

    public void InvalidateAll() =>
        CacheVersion = Interlocked.Increment(ref s_globalVersion);
}

public sealed class OutputPort<T> : Port
{
    struct Entry { public int Version; public T Value; }
    Entry[] _cache;                            // sized to maxConcurrentFlows
    readonly Func<Flow, T> _compute;
    readonly bool _shouldCache;

    internal void Bake(int maxFlows)
    {
        if (_cache.Length < maxFlows)
            _cache = new Entry[maxFlows];
    }

    public T Read(Flow flow)
    {
        if (!_shouldCache) return _compute(flow);
        ref var e = ref _cache[flow.Index];
        if (e.Version == flow.CacheVersion) return e.Value;
        e.Value   = _compute(flow);
        e.Version = flow.CacheVersion;
        return e.Value;
    }
}
```

### Why this works

- **Typed cache** → no boxing on value-type stores, no cast on reads.
- **Array index instead of dict TryGetValue** → ~10× cheaper lookup.
- **Version-counter invalidation** → `InvalidateAll` is `O(1)` (one
  interlocked increment), no clearing of any structure. Stale entries
  are detected lazily by `entry.Version != flow.CacheVersion`.
- **Global counter, not per-flow** → guarantees no two version values
  ever collide across flow lifecycles, so cached entries from a
  discarded flow never alias a fresh flow that reuses the same index.
- **Pre-warm at bake time** → each port allocates `Entry[maxFlows]`
  once during graph construction. Steady-state hot path is
  zero-allocation.

### What this is *not*

- Not a generator change. The generator continues to emit
  `new OutputPort<T>(...)` and `Ports.Add(...)`. Only the port's
  internals change.
- Not a port API change for users. `port.Read(flow)` keeps the same
  signature and semantics.
- Not a multi-threading rework. Flows still run on a single thread
  each; the global counter is `Interlocked` only because nothing
  prevents two runners from creating flows concurrently.

## Evidence

Spike: `Assets/Sandbox/PortCacheSpike/`. Same harness as
`PropertiesSpike` (`Bench.Measure`, 20 samples × 10 000 iter,
EditMode-Mono).

| Path                                  | Current (dict) | Proposed (array) | Speedup |
| ------------------------------------- | -------------- | ---------------- | ------- |
| **Cache miss** (3 reads, recompute)   | 455.71 ns      | **59.58 ns**     | **7.6×** |
| **Cache hit** (3 reads, warm cache)   | 137.85 ns      | **30.53 ns**     | **4.5×** |
| **Allocs/op** (cache miss path)       | 2              | **0**            | —        |

Cache-hit numbers matter most: in real graph execution, the cache hit
path dominates because invalidation only happens at run boundaries and
loop iteration starts (see `GraphRunner.cs:39, 50`,
`Loop.cs:28`). Within a run, every fan-out from one output to multiple
downstream nodes is hits.

For a port read 5× in a run (1 miss + 4 hits):

- Current:  ≈ 1×455 + 4×46 = **639 ns**
- Proposed: ≈ 1×60 + 4×10  = **100 ns**

Roughly **6× faster** in realistic per-run accounting, with allocations
eliminated.

### Correctness tests in the spike

- `IndexedFlow_FirstRead_ComputesValue` — cold read computes.
- `IndexedFlow_SecondRead_ReturnsCached` — warm read returns cached.
- `IndexedFlow_AfterInvalidate_RereadsValue` — `InvalidateAll` forces
  recompute.
- `IndexedFlow_TwoFlows_DontConflict` — two flows with different
  indices have isolated caches even on the same port.
- `IndexedFlow_IndexReuse_DoesNotReturnStaleValue` — flow B reusing
  flow A's index does not see A's stale value (catches the
  index-reuse-with-version-collision bug; passes with the global
  counter).
- `IndexedFlow_VersionsAreUniqueAcrossFlows` — locks down the
  no-collision invariant.
- `IndexedFlow_InvalidatePicksUniqueVersion` — `InvalidateAll` always
  produces a previously-unseen version.
- `IndexedPort_ZeroAllocAfterWarmup` — gates GC at zero on the hot path.

## Migration plan

Five steps, each independently mergeable.

1. **Add flow-index pooling to `GraphRunner`.**
   - New `internal int AcquireFlowIndex()` / `ReleaseFlowIndex(int)`
     on `GraphRunner`, backed by a stack of free indices.
   - Configurable `MaxConcurrentFlows` (default e.g. 8). Acquire
     throws / falls back to dict-mode if exceeded — see open question 1.
   - `Flow` constructor takes an index from the runner; `Flow`
     dispose / completion path returns it.
   - `Flow.InvalidateAll` switches to the global version counter.
   - All existing tests should still pass — this step is invisible to
     callers.

2. **Add `Bake(int maxFlows)` to `OutputPort<T>`.**
   - `GraphRunner` (or `BakedGraph`) calls it on every port during
     `Initialize()` after `MaxConcurrentFlows` is known.
   - Until step 3, the port still uses the old `Flow._cache` path —
     this just allocates the array.

3. **Switch `OutputPort<T>.Read` to the array path.** Remove
   `Flow.ReadCached` and `Flow._cache`. This is the user-visible perf
   change. Run the existing port-related test suites to confirm no
   regressions.

4. **Apply same treatment to other cached structures on `Flow`** if
   any equivalent dictionaries exist (`_slots` is `Dictionary<object,
   object>` and is a candidate; investigate separately — it's a
   different access pattern, may not benefit from indexing).

5. **Promote the spike's benchmarks into the regular regression
   suite** at `Tests/Performance/PortCacheRegression.cs` so the
   numbers don't drift back.

## Risks & open questions

1. **What's the right `MaxConcurrentFlows` default?**
   The current code allows unbounded concurrent flows on one runner.
   Capping at e.g. 8 is fine for game logic but might surprise
   simulation-style users running many flows in parallel.
   *Mitigation:* if the cap is hit, fall back to a dictionary path
   for the overflowing flow (slow but correct), or grow the array
   geometrically (correct but allocates).

2. **Memory cost.**
   Each cached `OutputPort<T>` now holds `maxFlows × sizeof(Entry<T>)`.
   For a graph with 200 ports and maxFlows=8 and a typical 32-byte
   payload that's ~50 KB worst case — negligible. Worth measuring on
   real graphs before locking in.

3. **Version-counter overflow.**
   `int` overflows after 2³¹ events. At 1 000 invalidations/sec that's
   ~24 days of continuous runtime. Not a concern for game sessions,
   would be for 24/7 server processes. *Mitigation:* `long` if anyone
   actually cares; the int compare is one cycle either way on 64-bit.

4. **Multi-runner / cross-runner flows.**
   The global counter is process-wide. This is correct for safety but
   means two runners' flows share a counter. Fine — there's no
   contention, just an `Interlocked.Increment`.

5. **Does `Flow` still own the cache philosophically?**
   With this change, the cache lives on the ports, not on the flow.
   `flow.InvalidateAll()` becomes a "version-bump" instead of a
   "clear my dictionary." That's semantically equivalent but
   conceptually cleaner: ports become self-caching, flow becomes
   pure context.

## Non-goals

- Removing the `InputPort` indirection. There's a further ~30–60 ns
  savings possible by skipping the `_source.Read(flow)` hop, but
  that's a separate spike and a bigger API change.
- Replacing the `Func<Flow, T>` compute lambda with something
  delegate-free. Same — separate spike.
- Burst-compiling port reads. Not a goal; ports remain managed.

## Decision needed

Approve this as the next implementation task in GraphFlow runtime, or
push back on the shape (e.g. dict-per-port instead of array-per-port,
per-flow generation instead of global counter, etc.).

If approved, step 1 of the migration plan is the smallest mergeable
unit — flow-index pooling without changing cache semantics — and
should land before steps 2–3.
