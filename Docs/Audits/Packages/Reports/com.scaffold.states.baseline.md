# `com.scaffold.states` — performance baseline notes

## Status

- **Phase 0** medians live in [`Assets/Benchmarks/States/baselines.json`](../../../../Assets/Benchmarks/States/baselines.json). Captured 2026-05-03 on **Editor PlayMode Mono** (Unity `6000.3.11f1`, Windows, AMD Ryzen 9 7945HX).
- Source export: [`Assets/Benchmarks/States/results-phase0-playmode.json`](../../../../Assets/Benchmarks/States/results-phase0-playmode.json).
- **Standalone IL2CPP lane: TBD.** Adding it is the only path to recovering trustworthy `Allocated` (bytes) numbers — see *Byte counter caveat* below.
- Audit: [`Docs/Audits/Packages/com.scaffold.states.md`](../com.scaffold.states.md). Plan: [`Plans/com.scaffold.states-refactor-ExecPlan.md`](../../../../Plans/com.scaffold.states-refactor-ExecPlan.md).

## Byte counter caveat

`Bench.ByteSource` resolved to `None` on this lane. The Editor process — under both EditMode and PlayMode Mono — does not advance `GC.GetAllocatedBytesForCurrentThread`; the per-thread allocator hook is inactive because Unity's native profiler frame-pump only fully wires up under a deployed Player. `GC.GetTotalMemory(false)` was probed second and also failed to advance under the harness's micro-allocation probe. Result: the `Allocated` (bytes) column reads 0 across the board.

Confirmed via web research and Unity issue tracker (IN-46213, status: *By Design* for Editor Mono). The two real workarounds are:

1. **Standalone IL2CPP test player** — `BuildPlayer` with `IncludeTestAssemblies` + `-runTests -testPlatform StandaloneWindows64 -scripting-backend IL2CPP`. Adds ~30s build cost per run; gives correct bytes. Future CI lane.
2. **`GC.TryStartNoGCRegion` + `GC.GetTotalMemory` delta** — would require patching `Bench.Measure`. Has a region-size ceiling. Risk of "still 0 for the heaviest tests."

For Phase 0 → Phase 7, **trust `AllocCount`**. The audit's success gates ("zero allocations on `Bench.NoAllocations` companion tests", "AllocCount = 0 on the dispatcher path") are stated in alloc-count terms anyway.

## How to capture numbers

1. Open the project in Unity 6000.x.
2. Verify `com.unity.test-framework.performance` resolves.
3. Run from repo root:

       pwsh -NoProfile -File ".agents/scripts/run-unity-tests.ps1" -TestPlatform PlayMode -PerformanceTestResultsPath "Assets/Benchmarks/States/results-phase<N>-playmode.json"

4. Re-export medians into `baselines.json` (per-phase) — see `Plans/com.scaffold.states-refactor-ExecPlan.md` §0.3 / §7.3.

## Benchmark suite

| Test class | Intent |
|------------|--------|
| `StoreExecuteBenchmarks` | `Execute_SingleMutator_OneSlice` (registry path) + `Execute_TypedMutator_OneSlice_NoRegistry` (registry-free floor). Phase 5 should pull the registry path down to the typed floor. |
| `StoreEnumerateAllBenchmarks` | `EnumerateAll_OneTypeBucket_1k` + `GetAll_OneTypeBucket_1k` — current `O(N)` `FillSlices` walk. Phase 3 (secondary index) should drop ns/op ≥3× and AllocCount → 0. |
| `NotifyBenchmarks` | `Notify_50Subs_NoMutation` non-mutating fanout cost. Phase 2 must keep this flat while gaining inline-unsubscribe correctness. |
| `MutatorRegistryBenchmarks` | `TryGet_KnownKey_50TypesRegistered` / `TryGet_UnknownKey_50TypesRegistered` — internal dispatch lookup floor. Reference for Phase 5. |
| `ReferenceEqualityBenchmarks` | `Equals_VirtualDispatch_VsReferenceNull` vs `ReferenceEquals_VsReferenceNull` — proves the §4.13 cliff (Phase 1 #6 swap). |
| `SubscribeBenchmarks` | `Subscribe_PerCall_CachedDelegate` — `Ledger.Add` overhead with the closure alloc held outside the measurement (Phase 1 #4 fix). |

## Regression tests (correctness, currently `[Ignore]`-tagged)

| File | Coverage |
|------|----------|
| `Assets/Benchmarks/States/StoreEnumerateAllReentrancyTests.cs` | Subscriber-triggered re-entry into `GetAll<TState>` while outer caller iterates `EnumerateAll<TState>` corrupts the iteration (audit §4.5). Un-ignore in Phase 2. |
| `Assets/Benchmarks/States/StateEventHandlerInlineUnsubscribeTests.cs` | Subscriber unsubscribes itself during `Notify` and triggers `InvalidOperationException: Collection was modified` (audit §4.11). Un-ignore in Phase 2. |

---

## Phase 0 medians (Editor PlayMode Mono, n=20 per group)

| Benchmark | Time (ns) | AllocCount | Allocated (B)\* |
|-----------|----------:|-----------:|----------------:|
| `MutatorRegistryBenchmarks.TryGet_KnownKey_50TypesRegistered` | 56.21 | 0.00 | 0.0 |
| `MutatorRegistryBenchmarks.TryGet_UnknownKey_50TypesRegistered` | 36.31 | 0.00 | 0.0 |
| `NotifyBenchmarks.Notify_50Subs_NoMutation` | 1,889.45 | 1.00 | 0.0 |
| `ReferenceEqualityBenchmarks.Equals_VirtualDispatch_VsReferenceNull` | 107.67 | 0.00 | 0.0 |
| `ReferenceEqualityBenchmarks.ReferenceEquals_VsReferenceNull` | 3.05 | 0.00 | 0.0 |
| `StoreEnumerateAllBenchmarks.EnumerateAll_OneTypeBucket_1k` | 246,811.00 | 7.01 | 0.0 |
| `StoreEnumerateAllBenchmarks.GetAll_OneTypeBucket_1k` | 203,144.00 | 7.01 | 0.0 |
| `StoreExecuteBenchmarks.Execute_SingleMutator_OneSlice` | 2,843.79 | 5.00 | 0.0 |
| `StoreExecuteBenchmarks.Execute_TypedMutator_OneSlice_NoRegistry` | 2,530.06 | 4.00 | 0.0 |
| `StoreExecuteBenchmarks.Execute_ValuePayload_OneSlice` | 2,977.66 | 7.00 | 0.0 |
| `StoreExecuteBenchmarks.Execute_TypedMutator_ValuePayload_OneSlice_NoRegistry` | 2,531.57 | 4.00 | 0.0 |
| `SubscribeBenchmarks.Subscribe_PerCall_CachedDelegate` | 258.08 | 1.00 | 0.0 |

\* See *Byte counter caveat* — bytes column is unrecoverable on this lane. **Trust `AllocCount`.**

## Headline observations vs. plan targets

- **`Execute_SingleMutator_OneSlice` — 5 allocs/call** (reference-type payload). **`Execute_ValuePayload_OneSlice` — 7 allocs/call** (struct payload, `readonly record struct ValueCombinedTickPayload`). The struct path *allocates more* than the reference path because of the boxing in `MutatorRunner.RunMutatorBindingsWithoutCommit` + `(TPayload)payload` cast in `RegisteredMutator.Apply`. This is the audit §4.4 cliff in numbers; Phase 5's source-gen `MutatorDispatcher` (per the rebased plan, an `IMutatorDispatcher` cross-assembly seam) should drop both to **0 allocs/call**.
- **Typed floor: `Execute_TypedMutator_*_NoRegistry` — 4 allocs/call** for both reference and value payloads. Confirms ~1 alloc is registry-attributable on the reference path and ~3 are boxing-attributable on the value path. Phase 1 + Phase 5 together close the gap.
- **`EnumerateAll_OneTypeBucket_1k` — 7 allocs/call, 247µs.** Phase 3 (`Dictionary<Type, List<BaseSlice>>` secondary index + struct enumerator) must drop AllocCount → 0 and ns/op ≥3× (target ~80µs).
- **`ReferenceEquals_VsReferenceNull` — 3.05 ns vs `Equals_VirtualDispatch_VsReferenceNull` — 107.67 ns.** ~35× cliff, confirming the audit §4.13 / Phase 1 #6 swap is real.
- **`Notify_50Subs_NoMutation` — 1 alloc/call** (the `IEnumerator<>` boxing in `NotifyReferenceSubscriptions`, audit §4.11). Phase 2 snapshot-then-iterate target: 0 allocs/call.
- **`MutatorRegistry.TryGet` — 56 ns hit / 36 ns miss, 0 alloc.** Solid Dictionary baseline; serves as the "before" floor for Phase 5's source-gen dispatcher.

---

End of baseline notes.
