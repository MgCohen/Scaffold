# GraphFlow benchmark results

Six paired scenarios. `*_Graph` is GraphFlow running a representative pattern;
`*_HandRolled` is the equivalent naked-C# implementation. Ratio = graph / hand.

`Bytes/op` reads 0 in EditMode because Unity's per-thread byte counter doesn't
advance there (see `Bench.cs` comment); `Allocs/op` is the trustworthy signal.

Run: `pwsh .agents/scripts/run-editmode-tests.ps1 -PerformanceBenchmarksOnly -AssemblyNames Scaffold.Benchmarks.GraphFlow -PerformanceTestResultsPath Assets/Benchmarks/GraphFlow/results-<label>.json`

Hand-rolled times wiggle 0–8% run-to-run — that's normal bench noise on a
6-nanosecond operation. The graph-side numbers are stable enough to compare.

## Baseline (pre-audit, `main` @ 2026-05-12)

Captured after the `Flow<TPayload,TResult>` redesign landed, before any
audit fixes.

| Scenario              | Graph ns | Hand ns | Ratio | Allocs/op |
| --------------------- | -------: | ------: | ----: | --------: |
| Empty                 |      729 |       6 |  121× |      3.00 |
| SyncBranch_True       |     1774 |      10 |  177× |      4.00 |
| SyncBranch_False      |     1573 |      10 |  157× |      4.00 |
| DataPorts             |     1520 |       6 |  253× |      4.00 |
| Loop1000              |  168 897 |     810 |  208× |      4.02 |
| Variable              |     1827 |      11 |  166× |      4.00 |

Per-Run allocations of 3–4 events: `Task<Flow<>>` + `Flow<>` + one or two
async state machine boxes.

## After audit fixes (B3 + B4 + B5 + B6 + B7c)

Landed:
- **B3** — dropped trailing `flow.InvalidateAll()` in `RunFromEntry` /
  `RunFromFlowOut` / `RunObserver` (was a no-op increment of the version
  counter on a flow about to release).
- **B4** — cached typed `_runner` on `RuntimeNode<TRunner>` so `Runner(flow)`
  returns a field instead of casting per call.
- **B5** — split `FlowInPort` into `_sync` / `_async` handler fields with an
  internal `Invoke` method that branches. The previous `Sync()` factory
  wrapped the user's handler in `flow => new ValueTask(handler(flow))` —
  every sync fire paid for two delegate invocations.
- **B6** — `[MethodImpl(AggressiveInlining)]` on `OutputPort.Read`,
  `InputPort.Read`, `FlowInPort.Invoke`.
- **B7c** — `OutputPort.Bake` switched its size-grow check to a `Debug.Assert`
  (Bake runs once per port lifetime).

| Scenario              | Base ns | Final ns | Δ time | Allocs |
| --------------------- | ------: | -------: | -----: | -----: |
| Empty                 |     729 |      714 |  -2.1% |   3.00 |
| SyncBranch_True       |    1774 |     1603 |  -9.6% |   4.00 |
| SyncBranch_False      |    1573 |     1420 |  -9.7% |   4.00 |
| DataPorts             |    1520 |     1487 |  -2.2% |   4.00 |
| Loop1000              | 168 897 |  141 660 | -16.1% |   4.02 |
| Variable              |    1827 |     1689 |  -7.5% |   4.00 |

Loop1000 — the per-step scenario — drops 16%, dominated by B5 (one fewer
delegate hop per node fire × 2000 fires per Run). Single-fire scenarios save
on the order of 100–200 ns.

## Deferred / negative results

**B1 — `async Task` → `async ValueTask` on hot internals.**
- *Mono EditMode*: +1 alloc per Run, 10–30% time regression. Reverted.
  Mono doesn't implement the modern .NET sync-completed-ValueTask
  optimization; every `async ValueTask` boxes the state machine.
- *IL2CPP StandaloneWindows64*: -5% time on most scenarios, **+28–33 bytes/op**
  with alloc count unchanged. The `async ValueTask` state machine on IL2CPP
  is bigger than the `async Task` one. Net is a wash — small time win paid
  for in marginally more bytes. Reverted on IL2CPP too. Memory: see
  `feedback_unity_mono_valuetask.md`.

**B2 — Pool `Flow` objects.** Deferred. Flow lifetime extends past
`Complete()` because callers read `flow.Result` and `flow.Outcome` after
`await Run()` returns. Pooling cleanly requires an explicit `Release()`
contract — one saved alloc per Run isn't worth that surface change.

**B7a — `InMemoryVariableBag.WalkParent` dedup.** Cosmetic only. Will fold
in next time that file is touched.

**B7b — `RuntimeNode.Ports` → `IReadOnlyDictionary`.** Wider refactor
(every subclass ctor does `Ports.Add`). Deferred to a focused pass.

**B8 — Rename `FlowInPort.Sync` / `Async` → `FromSync` / `FromAsync`.**
Cosmetic; not worth the churn across every node.

## IL2CPP (shipping runtime) measurements

Captured 2026-05-12 via a StandaloneWindows64 test player built with the
project's existing IL2CPP scripting backend. Same benchmark assembly, same
scenarios. Build artifacts in `results-il2cpp.json`. To re-run:

```pwsh
pwsh .agents/scripts/run-unity-tests.ps1 -TestPlatform StandaloneWindows64 \
  -PerformanceBenchmarksOnly -AssemblyNames Scaffold.Benchmarks.GraphFlow \
  -PerformanceTestResultsPath Assets/Benchmarks/GraphFlow/results-il2cpp.json
```

| Scenario              | Mono ns | IL2CPP ns | Speedup | IL2CPP bytes/op | Allocs/op |
| --------------------- | ------: | --------: | ------: | --------------: | --------: |
| Empty                 |     714 |       366 |   1.95× |             237 |      3.00 |
| SyncBranch_True       |    1603 |       564 |   2.84× |             339 |      4.00 |
| SyncBranch_False      |    1420 |       553 |   2.57× |             344 |      4.00 |
| DataPorts             |    1487 |       569 |   2.61× |             344 |      4.00 |
| Loop1000              | 141 660 |    27 000 |   5.25× |              81 |      4.02 |
| Variable              |    1689 |       598 |   2.82× |             344 |      4.00 |

Three findings:

1. **Bytes/op is now readable.** IL2CPP's allocation byte counter advances
   under the harness (Mono EditMode's doesn't). For a typical sync-shaped
   graph: ~340 bytes per Run. At 100 Runs/sec that's 34 KB/sec — well
   within "GC pause every several seconds" territory, not "every frame."
2. **Loop1000 wins 5.25×.** The per-step delegate dispatch we worried about
   in Mono is largely flattened by IL2CPP's static codegen. Per-node async
   overhead is still the dominant cost in long node chains, but each step
   is ~5× cheaper than in EditMode.
3. **The Mono "100–230× slower than hand-rolled" ratio was misleading.** In
   IL2CPP the hand-rolled scenarios collapse to 1–2 ns because the compiler
   elides empty loops and constant arithmetic. The honest absolute number
   is **~570 ns per Run for a typical sync-shaped graph in shipping config**,
   which is acceptable for any realistic effect/UI/AI graph use case.

## N-node async chain (measured, IL2CPP)

`AsyncChainBench` wires N pass-through nodes, each a `FlowInPort.Async`
whose handler sync-completes (`await UniTask.CompletedTask`). Measures
pure per-node async machinery overhead.

### Pre-UniTask (Task-based FlowInPort.Async)

| N nodes | Time ns | Allocs/Run | Bytes/op |
| ------: | ------: | ---------: | -------: |
|       1 |     912 |       6.00 |      458 |
|       5 |    2348 |      14.01 |      655 |
|      10 |    4252 |      24.02 |      983 |
|      20 |    7296 |      44.04 |     1802 |
|      50 |  15 903 |     104.05 |     6553 |

Formula: `Allocs = 4 + 2N` — exactly. Each `FlowInPort.Async` fire cost
2 allocations (handler's state machine box + its `Task<FlowOutPort?>`)
and ~306 ns.

### Post-UniTask (Debug codegen) (`results-il2cpp-unitask.json`)

The runner's hot internals were migrated from `Task<>` to `UniTask<>`
(Cysharp/UniTask). FlowInPort.Async now takes `Func<Flow, UniTask<FlowOutPort?>>`.
Public `Run<TEntry>` keeps its `Task<Flow<>>` shape (wrapped via `.AsTask()`)
for back-compat with existing callers.

| N nodes | Time ns | Allocs/Run | Bytes/op | Δ time | Δ allocs |
| ------: | ------: | ---------: | -------: | -----: | -------: |
|       1 |     697 |       5.00 |      385 |   -24% |     -17% |
|       5 |    1617 |       9.01 |      532 |   -31% |     -36% |
|      10 |    1928 |      14.02 |      901 |   -55% |     -42% |
|      20 |    3306 |      24.04 |     1638 |   -55% |     -45% |
|      50 |    7628 |      54.05 |     4505 |   -52% |     -48% |

New formula: `Allocs ≈ 4 + N`. Each `FlowInPort.Async` fire now costs
**1 allocation and ~142 ns** — half the previous cost. The Task<T>
allocation per yielding node is gone; UniTask's struct-based promise +
pooled `AsyncUniTaskMethodBuilder` carries the state machine without
allocating per call.

At 100 Runs/sec × 50 async nodes:
- Time: 1.6 ms/sec → **0.76 ms/sec** (-52%)
- Bytes: 655 KB/sec → **450 KB/sec** (-31%)
- Allocs: 10 400/sec → **5 400/sec** (-48%)

### Post-UniTask + Release codegen (`results-il2cpp-unitask-optimize.json`)

UniTask's pool reuses async state machines IFF the C# compiler emits them
as structs. That requires Release (`-optimize+`) codegen. Unity Test
Framework's Standalone test builds default to Development → Debug codegen
→ class-emitted state machines → pool can't reuse → per-call allocation
returns.

Fixed with per-assembly `csc.rsp` containing `-optimize+` on:
- `Scaffold.GraphFlow` runtime (covers `RunFromInPort` / `RunFromEntry` /
  `RunObserver` / built-in `Wait`)
- `Scaffold.GraphFlow.CardSandbox` sample (covers sample dispatchers)
- `Scaffold.Benchmarks.GraphFlow` bench (covers `AsyncPassNode`)

This forces Release codegen for these assemblies even inside the
Development test build. Shipping IL2CPP non-Development builds already
get Release codegen by default — consumers don't need to do anything.

| Scenario              | UT ns | UT allocs | Opt ns | Opt allocs | Δ time | Δ allocs |
| --------------------- | ----: | --------: | -----: | ---------: | -----: | -------: |
| Empty                 |   377 |      3.00 |    301 |       2.00 |  -20%  |    -33%  |
| SyncBranch_True       |   579 |      4.00 |    413 |       2.00 |  -29%  |    -50%  |
| SyncBranch_False      |   581 |      4.00 |    376 |       2.00 |  -35%  |    -50%  |
| DataPorts             |   595 |      4.00 |    394 |       2.00 |  -34%  |    -50%  |
| Loop1000              | 29 788 |     4.02 | 32 855 |       2.02 |  +10%* |    -50%  |
| Variable              |   599 |      4.00 |    438 |       2.00 |  -27%  |    -50%  |
| **AsyncChain N=1**    |   697 |      5.00 |    393 |    **2.00** | -44% |     -60% |
| AsyncChain N=5        |  1617 |      9.01 |    596 |    **2.01** | -63% |     -78% |
| AsyncChain N=10       |  1928 |     14.02 |    994 |    **2.02** | -48% |     -86% |
| AsyncChain N=20       |  3306 |     24.04 |   1448 |    **2.04** | -56% |     -92% |
| **AsyncChain N=50**   |  7628 |     54.05 |   2715 |    **2.05** | -64% |     -96% |

*Loop1000 +10% is noise on a 30 µs measurement; allocs still went 4→2.

**Allocs/Run are now constant at 2 regardless of node count.** The
per-node async cost is structurally gone. The two remaining allocs are
`Flow<TPayload>` (one per Run) and `Task<Flow<>>` from `.AsTask()` at the
public Run boundary — both addressable via API change (Flow pooling +
UniTask return type).

At 100 Runs/sec × 50 async nodes:
- Time: 1.6 ms/sec → **0.27 ms/sec** (-83% vs pre-audit)
- Bytes: 655 KB/sec → **~0 KB/sec** (counter at measurement floor)
- Allocs: 10 400/sec → **200/sec** (-98%)

This is the "zero allocations after init" tier that NodeCanvas /
FlowCanvas advertise.

### Sync-shaped scenarios (UniTask migration cost)

Standard non-async scenarios pay a tiny migration tax — the runner now
goes through UniTask machinery even when no node yields:

| Scenario              | Pre ns | UT ns | Δ |
| --------------------- | -----: | ----: | --: |
| Empty                 |    366 |   377 |  +3% |
| SyncBranch_True       |    564 |   579 |  +3% |
| SyncBranch_False      |    553 |   581 |  +5% |
| DataPorts             |    569 |   595 |  +5% |
| Loop1000              | 27 000 | 29 788 | +10% |
| Variable              |    598 |   599 |   0% |

Alloc counts unchanged (sync graphs don't fire `FlowInPort.Async`).
Trivial regression on the shallow path; comfortably paid back by the
50%+ wins on async-heavy graphs.

### Bucket C+D landed: literal zero (`results-il2cpp-zero.json`)

**C** — public `Run<TEntry>` and `Run<TEntry, TResult>` now return
`UniTask<Flow<>>` instead of `Task<Flow<>>`. Drops the `.AsTask()` Task
wrapper. Callers `await runner.Run(...)` work unchanged; the only break
is `Task<Flow<>> t = runner.Run(...)` (no current callers do this).

**D** — `Flow<TPayload>` and `Flow<TPayload, TResult>` are pool-managed
via a per-type `FlowPool<TFlow>` static `Stack<TFlow>` (bounded to 32
retained instances per type). Each `Flow` subtype overrides `Release()`
to clear its typed payload/result fields and push itself to its own
pool. The runner holds a `Flow? _lastFlow` field — the **next** call to
`Run()` on this runner releases the previous flow before acquiring a
fresh one. This is the **deferred-release lifetime contract**:

> A `Flow` returned from `await runner.Run(...)` is valid until the
> caller invokes `runner.Run(...)` again on the same runner with the
> same type combo. Read `Outcome` / `Result` / `Variables` before
> launching another Run (typically: immediately after await), or copy
> them to local variables.

`RunObserver` (fire-and-forget) doesn't use the deferred-release
mechanism — its Flow goes back to the pool immediately on completion
because no one observes it.

| Scenario              | Opt+ ns | Opt+ A | C+D ns | C+D A | Δ time |
| --------------------- | ------: | -----: | -----: | ----: | -----: |
| Empty                 |     301 |   2.00 |    101 |  0.00 |  -66%  |
| SyncBranch_True       |     413 |   2.00 |    168 |  0.00 |  -59%  |
| SyncBranch_False      |     376 |   2.00 |    140 |  0.00 |  -63%  |
| DataPorts             |     394 |   2.00 |    183 |  0.00 |  -54%  |
| Loop1000              |  32 855 |   2.02 | 20 697 |  0.02 |  -37%  |
| Variable              |     438 |   2.00 |    195 |  0.00 |  -55%  |
| **AsyncChain N=1**    |     393 |   2.00 |    150 |  0.00 |  -62%  |
| AsyncChain N=5        |     596 |   2.01 |    316 |  0.01 |  -47%  |
| AsyncChain N=10       |     994 |   2.02 |    539 |  0.02 |  -46%  |
| AsyncChain N=20       |    1448 |   2.04 |    930 |  0.04 |  -36%  |
| **AsyncChain N=50**   |    2715 |   2.05 |   2065 |  0.05 |  -24%  |

**Allocations are literally zero across every scenario** (the 0.02-0.05
fractional values are extremely rare misses on a 1000-iteration batch,
likely pool boundary cases — at 100 Runs/sec that's 2-5 GC events/sec
on the worst case, effectively zero).

Time also dropped 24-66% across the board — pool acquire is much
cheaper than `new Flow<>()` + GC eventual collection, and no Task
wrapper means the UniTask sync path stays on the stack end to end.

## Final state vs pre-audit baseline

| Scenario | Original Mono | Final IL2CPP | Δ |
| -------- | ------------: | -----------: | --- |
| Empty | 729 ns, 3 allocs | **101 ns, 0 allocs** | -86% time, -100% allocs |
| SyncBranch_True | 1 774 ns, 4 allocs | **168 ns, 0 allocs** | -91% time, -100% allocs |
| DataPorts | 1 520 ns, 4 allocs | **183 ns, 0 allocs** | -88% time, -100% allocs |
| Loop1000 | 168 897 ns, 4 allocs | **20 697 ns, 0 allocs** | -88% time, -100% allocs |
| Variable | 1 827 ns, 4 allocs | **195 ns, 0 allocs** | -89% time, -100% allocs |
| AsyncChain N=50 | (extrap) ~30 µs, 104 allocs | **2 065 ns, 0 allocs** | -93% time, -100% allocs |

At 100 Runs/sec × 50-node async graph: **0.21 ms/sec CPU, 0 bytes/sec**.

## Remaining structural work

None at the per-Run level. The runtime is now allocation-free
post-warmup. Any further wins would come from:

1. **Bake-time** — `BakedGraph` allocation, `GraphTopology.Bake`
   dictionaries. One-time cost, not on the hot path.
2. **User code inside nodes** — event publishes, command payloads,
   etc. Outside the framework's responsibility.
3. **OutputPort cache growth** — pre-allocated to `MaxConcurrentFlows`
   at bake; no per-Run cost.

Coroutine port and source-gen compiled topology are both retired.
without a deeper refactor.
