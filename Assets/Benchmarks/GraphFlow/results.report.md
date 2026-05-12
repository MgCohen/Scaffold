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
- *IL2CPP StandaloneWindows64* (`results-il2cpp-b1.json`): -5% time on most
  scenarios, **+28–33 bytes/op** with alloc count unchanged. The
  `async ValueTask` state machine on IL2CPP is bigger than the `async Task`
  one. Net is a wash — small time win paid for in marginally more bytes.
  Reverted on IL2CPP too. Memory: see `feedback_unity_mono_valuetask.md`.

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

## Remaining work — re-evaluated

The Mono numbers suggested a fundamental design problem; IL2CPP shows the
design is fine. Re-prioritized:

1. **Coroutine port** (replace `FlowInPort.Async` with IEnumerator factories,
   custom MonoBehaviour driver) still wins ~N allocs per Run on N-node async
   graphs. **Defer until profiling on real gameplay shows GC pressure.**
2. **Flow pooling** with result-snapshot API. **Defer** for the same reason.
3. **Source-gen "compiled topology"** rejected — it defeats the runtime-
   authored visual-scripting premise.

Current state is "ship-acceptable" on IL2CPP. Sync-shaped graph: ~570 ns +
~340 bytes per Run; N-node graph: ~570 ns + N × per-node cost.
without a deeper refactor.
