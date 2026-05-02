# `com.scaffold.maps` — performance baseline notes

## Status

- **Historic Phase 0** numbers lived in `Tests/Performance/baselines.json` (`editor_mono` from Unity 6000.x Editor Mono). After **Phases 2–5** (indexer refactor, no `Holder<T>`), those medians may **skew** benchmarks that changed allocation behavior — re-export and **replace `editor_mono`** when you have a Unity run.
- **IL2CPP** lane remains optional; record in a sibling section or JSON when available.

## How to capture numbers

1. Open the project in Unity **6000.x** (matches `package.json` unity field).
2. **Package Manager** — confirm **`com.unity.test-framework.performance`** resolves (`Packages/manifest.json`).
3. **Test Runner** → **Edit Mode**: **`Scaffold.Maps.Tests`** and **`Scaffold.Maps.Tests.Performance`**.
4. **Perf JSON**: batchmode **`-perfTestResults`** or `run-unity-tests.ps1` (see `.agents/scripts/README.md`). Merge medians into `Tests/Performance/baselines.json` **`editor_mono`**.

Optional: **`Tests/Performance/results-phase5.json`** — store the raw export beside `Docs/Audits/Packages/Reports/com.scaffold.maps.refactor-results.md`.

---

## Benchmark suite

| Test class | Intent |
|------------|--------|
| `MapIndexerValuesBench` | `Indexer.Values.Count` × 10k — post–Phase 2 expect **near-zero AllocCount**/byte (O(1) count). |
| `MapKeyEnumerationBench` | `GetPrimaryKeys` / `GetSecondaryKeys` × 10k — one **`HashSet<>`** per call (Phase 1 removed extra `List<>` copy). |
| `MapIndexerBulkRebuildBench` | Five indexers + bulk Add 10 / 100 / 1000. |
| `MapVsTupleDictBench` | `Map<int,int,string>` vs `Dictionary<(int,int),string>` — Add / TryGetValue / foreach. |

---

## Regression tests (correctness)

| Location | Coverage |
|----------|----------|
| `Tests/MapIndexerTests.cs` | Indexer rebuild, track/untrack, clear, duplicate name (`InvalidOperationException`), `GetIndexedValues` message, `IReadOnlyIndexer` via `TryGetIndexer`, key-only/value lines. |
| `Tests/MapReadOnlyAndGetAllTests.cs` | Read-only casts, GetAll, key sets. |

**Removed:** `MapIndexerCharacterizationTests` — scenarios merged into **`MapIndexerTests`** after Phase 3/4.

---

## Exit criteria (maintainer checklist)

- [ ] `Scaffold.Maps.Tests` green (Edit Mode).
- [ ] `Scaffold.Maps.Tests.Performance` green (`Indexer_Values_Count_PerRead_NoAlloc` included).
- [ ] `baselines.json` **`editor_mono`** refreshed from exported JSON after refactor settles.
- [ ] IL2CPP lane when pipeline supports it.
