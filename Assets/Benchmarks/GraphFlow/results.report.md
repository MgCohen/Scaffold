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

### Post-UniTask migration (`results-il2cpp-unitask.json`)

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

## Remaining work — re-evaluated post-UniTask

Position has improved materially. Deep async graphs are now at ~half
the cost — within the same ballpark as established Unity packages that
advertise "zero allocations after init."

1. **Flow pooling** with result-snapshot API. Saves 1 alloc per Run
   (the `Flow<>` instance) — still constant cost, increasingly negligible
   relative impact on deep graphs (was 1/24, now 1/14 for N=10).
   Probably defer unless we see a specific workload demanding it.
2. **Per-Run base allocs (4 → ~1-2 with pooling)** is the only remaining
   structural lever. Anything beyond requires either source-gen of the
   bake topology (rejected — defeats runtime authoring) or rewriting
   the public Run surface to non-Task.
3. **Coroutine port** is now redundant. UniTask delivers the same alloc
   shape via async/await syntax; no need to rewrite around IEnumerator.

Shallow-graph state: ~570 ns + ~340 bytes per Run (unchanged + 3%).
Deep-graph (50-node) state: **~7.6 µs + ~4.5 KB per Run, 54 allocs**.
without a deeper refactor.
