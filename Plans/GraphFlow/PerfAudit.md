# GraphFlow Perf & Quality Audit — Implementation Plan

Post-Flow<TPayload,TResult> follow-up. Two parallel tracks:

- **Track A — Benchmark harness.** Ship first so every later change has measured before/after numbers.
- **Track B — Fixes.** Ranked by expected impact. Land each as its own commit so the benchmark history shows the delta cleanly.

Notes:
- Unity is single-threaded; no thread-safety considerations.
- Every `Generators/` change needs the DLL redeploy step per `CLAUDE.md`.

---

## Track A — Benchmark harness

### A1. Benchmark harness + 6 scenarios

**Why.** Without a regression baseline we can't tell if a "perf fix" actually helped, and we can't quantify how far we sit from hand-rolled C#. We want a number per scenario that we re-measure after every change in Track B.

**Expected benefit.** Concrete numbers (ns/op, bytes/op) for graph vs hand-rolled across six representative shapes; a markdown file that gets diff-reviewed in PRs.

**Mechanics.**
- New asmdef `Scaffold.GraphFlow.PerformanceTests` under `Assets/Packages/com.scaffold.graphflow/Tests/Performance/`.
- Lightweight harness (no BenchmarkDotNet):
  - Warmup: 1000 iterations discarded.
  - Measure: 10 batches × 10000 iterations; record min/median ticks per batch.
  - Allocations: `GC.GetAllocatedBytesForCurrentThread()` delta over a fresh batch of 10000 iterations, divide by 10000.
- Each `[Test, Explicit]` runs **both** the graph and hand-rolled variant and pushes one row each into a shared `PerfReport.Rows`.
- `[OneTimeTearDown]` writes `Tests/Performance/results.md` (committed) with columns: `Scenario | Variant | ns/op | bytes/op | ratio vs hand-rolled`.

**Snippet — harness skeleton.**
```csharp
public static class PerfBench
{
    const int Warmup = 1000;
    const int Batches = 10;
    const int BatchSize = 10_000;

    public static PerfResult Measure(string scenario, string variant, Func<Task> run)
    {
        for (int i = 0; i < Warmup; i++) run().GetAwaiter().GetResult();

        long minTicks = long.MaxValue;
        var sw = new Stopwatch();
        for (int b = 0; b < Batches; b++)
        {
            sw.Restart();
            for (int i = 0; i < BatchSize; i++) run().GetAwaiter().GetResult();
            sw.Stop();
            if (sw.ElapsedTicks < minTicks) minTicks = sw.ElapsedTicks;
        }

        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < BatchSize; i++) run().GetAwaiter().GetResult();
        long bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / BatchSize;

        double nsPerOp = (minTicks * 1_000_000_000.0 / Stopwatch.Frequency) / BatchSize;
        return PerfReport.Push(scenario, variant, nsPerOp, bytes);
    }
}
```

**Scenarios (each paired graph + hand-rolled).**

| # | Scenario | Graph shape | Hand-rolled |
|---|----------|-------------|-------------|
| 1 | Empty | Entry → Return | `Task M() => Task.CompletedTask;` |
| 2 | Sync branch | Entry → Branch → Return | `if (cond) return r;` |
| 3 | Data ports | Entry → Add → Multiply → Return<int> | `(a+b)*c` |
| 4 | Loop N | Loop(1000) → counter → Done | `for (int i=0;i<1000;i++) sum++;` |
| 5 | Variable | SetVariable → GetVariable → Return | direct field |
| 6 | Async hop | One `await Task.Yield()` node | direct `await Task.Yield()` |

**Run.** `[Explicit]` so default Unity test runs skip it; `run-editmode-tests.ps1 -filter Performance` runs the suite.

---

## Track B — Fixes (ordered by expected impact)

### B1. Convert hot-path async methods from `Task` to `ValueTask`

**Why.** Every `async Task` method ships through `AsyncTaskMethodBuilder<Task>`, which boxes the state machine on the first call regardless of whether any await goes async. A fully-sync graph traversal currently boxes the state machine twice per Run (`Run*` + `RunFromInPort`) for no functional reason. `async ValueTask` only allocates the box if a real async pause occurs — sync graphs become alloc-free at the runner layer.

**Expected benefit.** ~80–120 bytes/op removed for any sync graph; for the empty-entry scenario this is most of the per-Run allocation. Latency improves a few hundred ns.

**Scope.** Internal helpers only. Public `Run<TEntry>` keeps returning `Task<Flow<TEntry>>` for caller ergonomics (most callers `await` once and want `Task`).

**Files.**
- `Runtime/Controller/GraphRunner.cs` — `RunFromInPort`, `RunFromEntry`, `RunFromFlowOut`, `RunObserver`.
- `Runtime/Ports/FlowInPort.cs` — already `ValueTask`. No change.

**Snippet.**
```csharp
// Before
async Task RunFromInPort(FlowInPort start, Flow flow) { ... }

// After
async ValueTask RunFromInPort(FlowInPort start, Flow flow) { ... }

// Public Run<TEntry> stays Task<Flow<TEntry>> for ergonomics — wrap:
public async Task<Flow<TEntry>> Run<TEntry>(...)
{
    var entry = ResolveEntry<TEntry>();
    var flow = NewFlow(payload, ct);
    await RunFromEntry(entry, flow);
    return flow;
}
```

**Test.** Re-run the benchmark suite; expect bytes/op to drop on every sync scenario.

---

### B2. Pool `Flow` objects

**Why.** Every `Run` allocates a fresh `Flow<TPayload>` (or `Flow<TPayload, TResult>`). The runner already caps concurrent flows via `MaxConcurrentFlows` and tracks free slots in `_freeFlowIndices` — that pool is half-built already. Backing each slot with a pre-allocated `Flow` instance and re-initializing payload + token + version on acquire makes Run effectively zero-alloc at the runner layer for sync graphs.

**Expected benefit.** Removes 32–64 bytes/op (the `Flow` itself) per Run. Combined with B1, the empty-entry scenario should be very close to zero allocations.

**Design constraint.** `Flow.Payload` is `readonly` today and `Flow<TPayload, TResult>.Result` is set once. Pooling means making both settable through an internal seam (analogous to `IFlowResult<TResult>`). The public surface stays read-only.

**Wrinkle.** A `Flow<TPayload>` pool keyed by type would need a separate pool per `(TPayload, TResult)` combination. Simpler: keep one pool per runner but key by `Type` → `Stack<Flow>`. Or store a `Flow[]` of generic `Flow` and reset slots polymorphically by exposing an internal `Reset(payload, token, cacheVersion)` on each subclass. Pick the latter — it stays type-safe at the call site.

**Snippet — sketch.**
```csharp
internal abstract class Flow
{
    internal void Reset(GraphRunner runner, CancellationToken ct, int cacheVersion)
    { /* clear _variables, Outcome=Running, etc. */ }
}

internal class Flow<TPayload> : Flow where TPayload : class
{
    public TPayload Payload { get; private set; } = null!;
    internal void Reset(TPayload payload, GraphRunner r, CancellationToken ct, int v)
    { Payload = payload; base.Reset(r, ct, v); }
}
```

Inside `GraphRunner`, replace `NewFlow<TPayload>` with `AcquireFlow<TPayload>` that pulls from a typed pool, calls `Reset`, and on `Complete()` returns to the pool.

**Test.** Benchmark expects bytes/op to drop further; functional tests in `Tests/` must all pass unchanged (no public API change).

---

### B3. Drop spurious `flow.InvalidateAll()` at end of run

**Why.** `RunFromEntry`, `RunFromInPort`, `RunFromFlowOut`, `RunObserver` all call `flow.InvalidateAll()` immediately before `flow.Complete()`. The flow is about to release its index and never be observed again, so bumping `CacheVersion` is dead work — it just increments `Runner._versionCounter` for nothing. Suspected vestige from before Flow/index pooling was in scope.

**Expected benefit.** Trivial perf (one int increment), but cleaner intent. Also: if we ever pool Flow (B2), the trailing invalidate must move into `Reset()` anyway, so deleting it now simplifies that refactor.

**Files.** `Runtime/Controller/GraphRunner.cs` lines ~118, 142, 168.

**Snippet.**
```csharp
// Before
try { if (dest != null) await RunFromInPort(dest, flow); flow.InvalidateAll(); return flow; }
finally { flow.Complete(); }

// After
try { if (dest != null) await RunFromInPort(dest, flow); return flow; }
finally { flow.Complete(); }
```

**Caveat.** Keep `flow.InvalidateAll()` inside `Loop.Continue` — that one is semantically necessary (it invalidates per-iteration data port reads).

**Test.** Existing tests pass. No benchmark expectation beyond noise.

---

### B4. Cache typed runner on `RuntimeNode<TRunner>`

**Why.** `RuntimeNode<TRunner>.Runner(flow)` casts `flow.Runner` to `TRunner` on every call. For nodes that dispatch through Runner often (CardSandbox-style command dispatchers), that's one cast per node fire. Caching the typed runner at `Initialize(TRunner)` removes the cast.

**Expected benefit.** Small — a cast is cheap but not free. Mostly a quality-of-implementation tightening. Will likely show up in scenario 2/3 numbers as a few-percent reduction.

**Files.** `Runtime/Nodes/RuntimeNode.cs`.

**Snippet.**
```csharp
public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
{
    TRunner _runner = null!;
    protected TRunner Runner => _runner;            // no flow arg needed
    protected TRunner GetRunner(Flow _) => _runner; // back-compat overload if call sites use Runner(flow)

    public sealed override void Initialize(GraphRunner runner)
    {
        _runner = (TRunner)runner;
        Initialize(_runner);
    }
    public virtual void Initialize(TRunner runner) { }
}
```

Migrate call sites from `Runner(flow).X` to `Runner.X`. Single-pass grep + edit.

**Test.** Existing tests pass.

---

### B5. Split `FlowInPort` sync vs async dispatch

**Why.** `FlowInPort.Sync(owner, name, handler)` today wraps the user's `Func<Flow, FlowOutPort?>` in `flow => new ValueTask<FlowOutPort?>(handler(flow))`. Every sync node-fire pays for two delegate invocations (outer wrap + inner handler). On a chain of N sync nodes that's 2N delegate calls instead of N.

**Expected benefit.** Should show up in scenario 2 (sync branch) and 4 (loop body). Estimate 10–20% on those scenarios.

**Design.** Store one of two delegates internally, dispatch via a method that picks the right path. Or — simpler — keep the `ValueTask`-returning seam and use `ValueTask.FromResult` directly without the extra closure (but the closure still captures `handler`, so the closure is the cost, not the wrap).

The cleaner fix is to make `Invoke` a non-delegate method on `FlowInPort` that branches internally:

**Snippet.**
```csharp
public sealed class FlowInPort : Port
{
    readonly Func<Flow, FlowOutPort?>? _sync;
    readonly Func<Flow, Task<FlowOutPort?>>? _async;

    FlowInPort(RuntimeNode owner, string name,
               Func<Flow, FlowOutPort?>? sync,
               Func<Flow, Task<FlowOutPort?>>? async)
    { Owner = owner; Name = name; _sync = sync; _async = async; }

    internal ValueTask<FlowOutPort?> Invoke(Flow flow) =>
        _sync != null
            ? new ValueTask<FlowOutPort?>(_sync(flow))
            : new ValueTask<FlowOutPort?>(_async!(flow));

    public static FlowInPort Sync(RuntimeNode o, string n, Func<Flow, FlowOutPort?> h)
        => new(o, n, h, null);
    public static FlowInPort Async(RuntimeNode o, string n, Func<Flow, Task<FlowOutPort?>> h)
        => new(o, n, null, h);
}
```

One delegate invocation in the sync path; matches the existing public API.

**Test.** Existing tests pass; benchmark scenarios 2 and 4 expected to improve.

---

### B6. `[MethodImpl(AggressiveInlining)]` on hot reads

**Why.** `OutputPort<T>.Read`, `InputPort<T>.Read`, and `FlowInPort.Invoke` are the per-step calls in every graph traversal. They're small enough to inline, but the JIT may not always choose to. AggressiveInlining is a hint, not a guarantee, but it's cheap to add and occasionally produces measurable wins for tiny hot methods.

**Expected benefit.** Small (single-digit %); only matters if the JIT was declining to inline. Worth trying because it's near-zero risk.

**Snippet.**
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public T Read(Flow flow) { ... }
```

**Test.** Benchmark — keep or revert based on numbers.

---

### B7. Polish — `WalkParent` dedup, `Ports` readonly, `Bake` assert

Grouped because each is small and none should move benchmark numbers. Land as one cleanup commit.

**B7a — `InMemoryVariableBag.WalkParent` dedup.** Two near-identical copies (generic + non-generic). Extract a single private helper that takes a `Func<IVariableHandle, IVariableHandle?>` selector, or keep both and just share the visited-set / cycle-walk logic. Low priority — only worth doing if we touch the file for another reason.

**B7b — `RuntimeNode.Ports` readonly.** Currently exposes the mutable `Dictionary<string, Port>`. After `Build` no one should mutate. Change the public surface to `IReadOnlyDictionary<string, Port>`; keep an internal `_ports` for `Add` during construction.

**B7c — `OutputPort<T>.Bake` assert.** The `if (_cache.Length < maxFlows)` guard is misleading since `Bake` runs exactly once. Change to:
```csharp
internal override void Bake(int maxFlows)
{
    if (!_shouldCache) return;
    Debug.Assert(_cache.Length == 0, "OutputPort.Bake called twice");
    _cache = new Entry[maxFlows];
}
```

**Test.** Existing tests pass.

---

### B8. Rename `FlowInPort.Sync` → `FromSync` (optional)

**Why.** The current name suggests the port itself is synchronous, but `Invoke` always returns `ValueTask`. `FromSync` / `FromAsync` is more honest about "you supply a sync handler / you supply an async handler".

**Expected benefit.** Readability only. No perf impact.

**Cost.** Touches every node that authors a `FlowInPort` — generated code, builtin nodes, tests, sample. Coordinated rename.

**Recommendation.** Skip unless we're already doing a wider API revamp. Cosmetic-only rename of a frequently-used API is rarely worth the churn.

---

## Sequencing

1. **A1** — harness + baseline numbers committed.
2. **B1** — async ValueTask. Re-measure.
3. **B2** — Flow pool. Re-measure.
4. **B3** — drop spurious InvalidateAll. Re-measure (mostly to confirm no regression).
5. **B5** — FlowInPort sync split. Re-measure.
6. **B4** — typed runner cache. Re-measure.
7. **B6** — AggressiveInlining. Keep changes only if numbers move.
8. **B7** — polish bundle.
9. **B8** — defer indefinitely unless we touch the surface for another reason.

After step 3 we should already be in striking distance of hand-rolled for sync graphs; the rest are increments.
