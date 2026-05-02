# `com.scaffold.maps` — Phase 0 performance baseline

**Status:** Harness and benchmarks are checked in; **numeric baseline rows are pending** a Unity Editor run (this repo snapshot has no Unity runner in CI). The file `Assets/Packages/com.scaffold.maps/Tests/Performance/baselines.json` is a placeholder until exported metrics are merged.

## How to capture numbers

1. Open the project in Unity **6000.x** (matches `package.json` unity field).
2. **Window → Package Manager** — confirm **`com.unity.test-framework.performance`** **3.2.0** resolves (`Packages/manifest.json`).
3. **Window → General → Test Runner** → **Edit Mode**.
4. Assembly filter: **`Scaffold.Maps.Tests`** (characterization) and **`Scaffold.Maps.Tests.Performance`** (benchmarks).
5. Run characterization tests (`MapIndexerCharacterizationTests`) — must be green.
6. Run performance tests (category Performance / tests marked `[Performance]`).
7. Export results JSON from the Test Runner (per `_benchmarking.md` Reporting section).
8. Feed exported JSON through **[PerformanceBenchmarkReporter](https://github.com/Unity-Technologies/PerformanceBenchmarkReporter)** or merge medians into `Tests/Performance/baselines.json` using your repo’s baseline schema.

### IL2CPP lane

Repeat in **Play Mode** with an **IL2CPP** standalone player build when your pipeline supports it. Record separately (e.g. `baselines.json` section or sibling file); canonical allocation profile is IL2CPP per `_benchmarking.md`.

---

## Benchmark suite (Phase 0)

| Test class | Intent |
|------------|--------|
| `MapIndexerValuesBench` | `Indexer.Values` cost — forces materialization via `.Count`; expect large bytes/op proportional to entry count × iterations today. |
| `MapKeyEnumerationBench` | `GetPrimaryKeys` / `GetSecondaryKeys` × 10k on 1000-entry map — expect HashSet + List allocation per call today. |
| `MapIndexerBulkRebuildBench` | Empty map + five indexers + bulk Add 10 / 100 / 1000. |
| `MapVsTupleDictBench` | `Map<int,int,string>` vs `Dictionary<(int,int),string>` — Add / TryGetValue / foreach. |

Sample groups reported by `Bench.Measure`: **Time** (ns/op), **Allocated** (bytes/op), **Gen0**, **Gen1**, **Gen2** per measurement window.

---

## Characterization tests (EditMode)

Located in `Tests/MapIndexerCharacterizationTests.cs` (assembly **`Scaffold.Maps.Tests`**).

| Test | Expected today |
|------|----------------|
| `IndexerValues_DoesNotReflectValueMutation_ByDesign_KeyOnlyPredicates` | Pass — membership unchanged by key; returned values reflect current holder value after `[index]` set. |
| `AddIndexer_DuplicateName_ThrowsArgumentException` | Pass — `Dictionary.Add` throws **`ArgumentException`**. Phase 3 → **`InvalidOperationException`**. |
| `GetIndexedValues_MissingName_ThrowsKeyNotFoundException` | Pass — indexer dictionary indexer throws **`KeyNotFoundException`**. Phase 3 → explicit message contract. |
| `Add_HalfKey_TPrimaryRefType_DefaultPrimaryNull_ThrowsArgumentNullException` | Pass — **`Add(TSecondary, TValue)`** uses default primary **`null`** for `Map<string,int,string>` → **`ArgumentNullException`** on **`Index`** ctor. Removed in Phase 1. |

---

## Results table (fill after Unity run)

_Paste median ns/op, bytes/op, Gen0/Gen1/2 per benchmark test name below._

| Benchmark | Time (ns/op) | Allocated (bytes/op) | Gen0 | Gen1 | Gen2 |
|-----------|--------------|----------------------|------|------|------|
| _(pending)_ | | | | | |

---

## Exit criteria checklist

- [ ] All `MapIndexerCharacterizationTests` green in Edit Mode.
- [ ] All `[Performance]` tests complete without harness exceptions.
- [ ] `baselines.json` updated from exported metrics (replace placeholder arrays).
- [ ] IL2CPP lane captured when available.
