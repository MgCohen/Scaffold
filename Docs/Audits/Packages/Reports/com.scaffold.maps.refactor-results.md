# `com.scaffold.maps` — refactor results (Phases 2–5)

**Change set:** Structural indexer rewrite (dictionary-backed `BaseMap` without `Holder<T>`), keyed `HashSet<Index<,>>` indexers, `IReadOnlyIndexer` + clearer exceptions, `IndexerValuesView` for allocation-friendly `Values` / `Count`, `MapIndexerTests` coverage.

**Measured numbers:** Not captured in this workspace (no Unity runner). After you run the perf suite in Unity, replace the qualitative table below with medians and attach or merge `Assets/Benchmarks/Maps/results-phase5.json`.

## Benchmark policy

See `Docs/Audits/Packages/_benchmarking.md`: **Allocated** must not regress >10% where the byte counter works; **AllocCount** remains the reliable Editor signal when bytes read 0; **Gen0** must not rise; **Time** >5% is review-only.

## Qualitative comparison (fill with real medians)

| Benchmark / signal | Phase 0 expectation | Post-refactor expectation |
|-------------------|--------------------|---------------------------|
| `MapIndexerValuesBench` — `.Values.Count` AllocCount | Large (list per op) | **~0** per op |
| `MapKeyEnumerationBench` | HashSet per call | Same (Phase 1 already dropped list copy) |
| `MapIndexerBulkRebuildBench` | Indexer `Track` per add | Similar or better (no holder list scans) |
| `MapVsTupleDictBench` — Map Add | Holder overhead | **Closer to tuple dict** |

## Artifacts

| File | Role |
|------|------|
| `Assets/Benchmarks/Maps/baselines.json` | Checked-in floor — **update medians** after Unity export |
| `Assets/Benchmarks/Maps/results-phase5.json` | Placeholder for last perf JSON export path |

## Tests

- **`MapIndexerTests`**: indexer rebuild, track/untrack, clear, duplicate name (`InvalidOperationException`), missing `GetIndexedValues` message, `TryGetIndexer` read-only contract, value-mutation contract.
- **`PerformanceAllocationVerificationTests.Indexer_Values_Count_PerRead_NoAlloc`**: asserts hot-path **Count** allocates nothing (AllocCount).
