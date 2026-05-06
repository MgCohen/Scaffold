# `com.scaffold.states` — performance baseline notes

## Status

- **Phase 0** medians live in [`Assets/Benchmarks/States/baselines.json`](../../../../Assets/Benchmarks/States/baselines.json). Captured 2026-05-03 on **Standalone IL2CPP test player** (Unity `6000.3.11f1`, Windows, AMD Ryzen 9 7945HX). Production-shape runtime — both `Time` and `Allocated` columns are trustworthy.
- Source export: [`Assets/Benchmarks/States/results-phase0-il2cpp.json`](../../../../Assets/Benchmarks/States/results-phase0-il2cpp.json).
- The earlier Editor PlayMode Mono export at `results-phase0-playmode.json` is retained for reference; it has 0 in the bytes column due to the dormant per-thread allocator hook documented under *Byte counter notes* below.
- Audit: [`Docs/Audits/Packages/com.scaffold.states.md`](../com.scaffold.states.md). Plan: [`Plans/com.scaffold.states-refactor-ExecPlan.md`](../../../../Plans/com.scaffold.states-refactor-ExecPlan.md).

## How to capture numbers

1. Open the project in Unity 6000.x.
2. Verify `com.unity.test-framework.performance` resolves and `Player Settings → Other → Scripting Backend (Standalone)` is set to **IL2CPP**.
3. Run from repo root:

       pwsh -NoProfile -File ".agents/scripts/run-unity-tests.ps1" -TestPlatform StandaloneWindows64 -PerformanceTestResultsPath "Assets/Benchmarks/States/results-phase<N>-il2cpp.json"

4. First IL2CPP build is slow (~5 min: transpile + MSVC compile + link). Subsequent runs are incremental.
5. Re-export medians into `baselines.json` (per-phase) — see `Plans/com.scaffold.states-refactor-ExecPlan.md` §0.3 / §7.3.

## Byte counter notes

`Bench.ByteSource` only resolves to a working counter under a deployed Player. The Editor process — under both EditMode and PlayMode Mono — does not advance `GC.GetAllocatedBytesForCurrentThread` because Unity's native profiler frame-pump is dormant. Confirmed via Unity issue tracker IN-46213 (status: *By Design* for Editor Mono). The Standalone IL2CPP lane is therefore the canonical baseline; PlayMode Mono is acceptable for AllocCount-only spot checks but should not be used for bytes assertions.

## Benchmark suite

| Test class | Intent |
|------------|--------|
| `StoreExecuteBenchmarks` | `Execute_SingleMutator_OneSlice` (registry path, ref payload) + `Execute_ValuePayload_OneSlice` (registry path, struct payload) + the typed-no-registry siblings. Phase 5 should pull both registry paths down to the typed floor and erase the struct boxing. |
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

## Phase 0 medians (Standalone IL2CPP Player, n=20 per group)

| Benchmark | Time (ns) | AllocCount | Allocated (B) |
|-----------|----------:|-----------:|--------------:|
| `MutatorRegistryBenchmarks.TryGet_KnownKey_50TypesRegistered` | 16.39 | 0.00 | 0 |
| `MutatorRegistryBenchmarks.TryGet_UnknownKey_50TypesRegistered` | 13.40 | 0.00 | 0 |
| `NotifyBenchmarks.Notify_50Subs_NoMutation` | 735.20 | 1.00 | 8 |
| `ReferenceEqualityBenchmarks.Equals_VirtualDispatch_VsReferenceNull` | 9.34 | 0.00 | 0 |
| `ReferenceEqualityBenchmarks.ReferenceEquals_VsReferenceNull` | 1.01 | 0.00 | 0 |
| `StoreEnumerateAllBenchmarks.EnumerateAll_OneTypeBucket_1k` | 42,613.50 | 7.01 | 204 |
| `StoreEnumerateAllBenchmarks.GetAll_OneTypeBucket_1k` | 37,541.50 | 7.01 | 245 |
| `StoreExecuteBenchmarks.Execute_SingleMutator_OneSlice` | 2,239.90 | 5.00 | 38 |
| `StoreExecuteBenchmarks.Execute_TypedMutator_OneSlice_NoRegistry` | 1,936.98 | 4.00 | 82 |
| `StoreExecuteBenchmarks.Execute_ValuePayload_OneSlice` | 2,245.62 | 6.00 | 67 |
| `StoreExecuteBenchmarks.Execute_TypedMutator_ValuePayload_OneSlice_NoRegistry` | 1,946.01 | 4.00 | 82 |
| `SubscribeBenchmarks.Subscribe_PerCall_CachedDelegate` | 167.74 | 1.00 | 31 |

## Headline observations vs. plan targets

- **`Execute_SingleMutator_OneSlice` — 5 allocs / 38 B/call** (reference-type payload). **`Execute_ValuePayload_OneSlice` — 6 allocs / 67 B/call** (struct payload, `readonly record struct ValueCombinedTickPayload`). The struct path allocates one extra event and ~29 extra bytes per call from the boxing in `MutatorRunner.RunMutatorBindingsWithoutCommit` + `(TPayload)payload` cast in `RegisteredMutator.Apply`. This is the audit §4.4 cliff in numbers; Phase 5's source-gen `MutatorDispatcher` (per the rebased plan, an `IMutatorDispatcher` cross-assembly seam) should drop both to **0 allocs / 0 B/call**.
- **Typed floor: `Execute_TypedMutator_*_NoRegistry` — 4 allocs / 82 B/call** for both reference and value payloads, identical. Confirms the registry path costs ~1 extra alloc on top, and that the typed-no-registry path's per-call bytes overhead is real overlay/scratchpad churn that Phase 2's reentrancy/buffer hygiene + Phase 3's indexed slice store should both whittle down.
- **`EnumerateAll_OneTypeBucket_1k` — 7 allocs / 204 B / 43µs.** Phase 3 (`Dictionary<Type, List<BaseSlice>>` secondary index + struct enumerator) must drop AllocCount → 0, bytes → 0, and ns/op ≥3× (target ~14µs).
- **`ReferenceEquals_VsReferenceNull` — 1.01 ns vs `Equals_VirtualDispatch_VsReferenceNull` — 9.34 ns.** ~9× cliff under IL2CPP (the JIT compresses the gap considerably vs Mono's ~35×). Audit §4.13 / Phase 1 #6 swap still a clear win at zero risk.
- **`Notify_50Subs_NoMutation` — 1 alloc / 8 B/call** (the `IEnumerator<>` boxing in `NotifyReferenceSubscriptions`, audit §4.11). Phase 2 snapshot-then-iterate target: 0 allocs / 0 B/call.
- **`MutatorRegistry.TryGet` — 16 ns hit / 13 ns miss, 0 alloc.** Solid Dictionary baseline; serves as the "before" floor for Phase 5's source-gen dispatcher (which should match or beat).
- **IL2CPP vs PlayMode Mono — ~6× faster on the hot paths**: `EnumerateAll` 247µs → 43µs, `Notify` 1889ns → 735ns, `Execute` 2843ns → 2240ns, `ReferenceEquals` 3ns → 1ns. Mono PlayMode is fine for fast iteration on AllocCount-only assertions; IL2CPP is the canonical baseline for any phase where the comparison report goes into a PR.

---

End of baseline notes.
