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

**B1 — `async Task` → `async ValueTask` on hot internals.** Tried; regressed
+1 alloc per Run and 10–30% time on every scenario. Unity Mono EditMode
doesn't implement the modern .NET sync-completed-ValueTask optimization, so
`async ValueTask` still boxes the state machine on the sync path. Reverted.
May still help on IL2CPP — not measured here. Memory: see
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

## Remaining gap to hand-rolled

Graph still runs 100–230× slower than hand-rolled in absolute terms. The
remaining cost is dominated by:

1. **Per-Run async machinery**: `Task<Flow<>>` + 1–2 async state-machine
   boxes per call. ~3–4 allocs/op unchanged. Can't be removed without
   either Flow pooling (deferred) or switching to a `ValueTask` runtime
   that supports the sync-fast-path (IL2CPP / .NET 6+).
2. **Per-step delegate dispatch**: `OutputPort._compute` and `FlowInPort._sync`
   are `Func<>` instances. Each `Read` / `Invoke` is one delegate call. The
   only path to remove this is source-gen — emit a non-virtual method on the
   node class that reads ports inline.

Both are tractable, but each is a separate project. Current state: any
sync graph scenario costs ~700 ns + ~1 µs per node fire. That's the floor
without a deeper refactor.
