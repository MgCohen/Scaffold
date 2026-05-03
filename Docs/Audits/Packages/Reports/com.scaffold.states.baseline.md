# `com.scaffold.states` — performance baseline notes

## Status

- **Phase 0** medians live in [`Assets/Benchmarks/States/baselines.json`](../../../../Assets/Benchmarks/States/baselines.json). Captured 2026-05-03 on Editor Mono (Unity `6000.3.11f1`, Windows, AMD Ryzen 9 7945HX).
- Source export: [`Assets/Benchmarks/States/results-phase0.json`](../../../../Assets/Benchmarks/States/results-phase0.json).
- IL2CPP lane: **TBD** — record alongside when CI supports it.
- Audit: [`Docs/Audits/Packages/com.scaffold.states.md`](../com.scaffold.states.md). Plan: [`Plans/com.scaffold.states-refactor-ExecPlan.md`](../../../../Plans/com.scaffold.states-refactor-ExecPlan.md).

## How to capture numbers

1. Open the project in Unity 6000.x.
2. Verify `com.unity.test-framework.performance` resolves.
3. Run from repo root:

       pwsh -NoProfile -File ".agents/scripts/run-unity-tests.ps1" -TestPlatform EditMode -PerformanceTestResultsPath "Assets/Benchmarks/States/results-phase<N>.json"

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

## Phase 0 medians (Editor Mono, n=20 per group)

| Benchmark | Time (ns) | AllocCount | Allocated (B)\* |
|-----------|----------:|-----------:|----------------:|
| `MutatorRegistryBenchmarks.TryGet_KnownKey_50TypesRegistered` | 60.02 | 0.00 | 0.0 |
| `MutatorRegistryBenchmarks.TryGet_UnknownKey_50TypesRegistered` | 38.89 | 0.00 | 0.0 |
| `NotifyBenchmarks.Notify_50Subs_NoMutation` | 1,829.25 | 1.00 | 0.0 |
| `ReferenceEqualityBenchmarks.Equals_VirtualDispatch_VsReferenceNull` | 101.94 | 0.00 | 0.0 |
| `ReferenceEqualityBenchmarks.ReferenceEquals_VsReferenceNull` | 3.22 | 0.00 | 0.0 |
| `StoreEnumerateAllBenchmarks.EnumerateAll_OneTypeBucket_1k` | 243,396.50 | 7.01 | 0.0 |
| `StoreEnumerateAllBenchmarks.GetAll_OneTypeBucket_1k` | 199,691.00 | 7.01 | 0.0 |
| `StoreExecuteBenchmarks.Execute_SingleMutator_OneSlice` | 2,809.89 | 5.00 | 0.0 |
| `StoreExecuteBenchmarks.Execute_TypedMutator_OneSlice_NoRegistry` | 2,480.86 | 4.00 | 0.0 |
| `SubscribeBenchmarks.Subscribe_PerCall_CachedDelegate` | 265.85 | 1.00 | 0.0 |

\* `Allocated` (bytes) reads zero across the board on this Editor Mono lane — `Bench.Measure`'s default byte source under-reports below its sample threshold here. **Trust `AllocCount` for Phase 0.** Re-record `Allocated` once the byte-source quirk is resolved (per `_benchmarking.md`) or pair with the IL2CPP lane.

## Headline observations vs. plan targets

- **`Execute_SingleMutator_OneSlice` — 5 allocs/call** today (Phase 0). Plan Phase 5 target: **0 allocs/call** via source-gen dispatcher. The typed floor (`Execute_TypedMutator_OneSlice_NoRegistry`) at 4 allocs/call shows ~1 alloc is registry-attributable; the rest comes from `MutatorRunner.RunMutatorBindingsWithoutCommit` enumerator boxing (§4.12) and overlay/scratchpad churn that Phase 2/5 hits.
- **`EnumerateAll_OneTypeBucket_1k` — 7 allocs/call, 243µs.** Phase 3 (`Dictionary<Type, List<BaseSlice>>` secondary index + struct enumerator) must drop AllocCount → 0 and ns/op ≥3× (target ~80µs).
- **`ReferenceEquals_VsReferenceNull` — 3.22 ns vs `Equals_VirtualDispatch_VsReferenceNull` — 101.94 ns.** ~32× cliff, confirming the audit §4.13 / Phase 1 #6 swap is real (not just a footgun-on-paper).
- **`Notify_50Subs_NoMutation` — 1 alloc/call** (the `IEnumerator<>` boxing in `NotifyReferenceSubscriptions`, audit §4.11). Phase 2 snapshot-then-iterate target: 0 allocs/call.
- **`MutatorRegistry.TryGet` — 60 ns hit / 39 ns miss, 0 alloc.** Solid Dictionary baseline; serves as the "before" floor for Phase 5's source-gen dispatcher (which should beat both).

---

End of baseline notes.
