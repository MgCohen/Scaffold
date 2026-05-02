# `com.scaffold.maps` — Phase 0 performance baseline

**Status:** Harness and benchmarks are checked in; **numeric baseline rows are pending** a Unity Editor run (this repo snapshot has no Unity runner in CI). The file `Assets/Packages/com.scaffold.maps/Tests/Performance/baselines.json` is a placeholder until exported metrics are merged.

## How to capture numbers

1. Open the project in Unity **6000.x** (matches `package.json` unity field).
2. **Window → Package Manager** — confirm **`com.unity.test-framework.performance`** **3.2.0** resolves (`Packages/manifest.json`).
3. **Window → General → Test Runner** → **Edit Mode**.
4. Assembly filter: **`Scaffold.Maps.Tests`** (characterization) and **`Scaffold.Maps.Tests.Performance`** (benchmarks).
5. Run characterization tests (`MapIndexerCharacterizationTests`) — must be green.
6. Run performance tests (tests marked `[Performance]`, assembly **`Scaffold.Maps.Tests.Performance`**).
7. **Machine-readable JSON** (for tools / baselines): from the repo root, run **`run-unity-tests.ps1`** with **`-PerformanceTestResultsPath`** (see `.agents/scripts/README.md`). Example:

   ```powershell
   pwsh -NoProfile -File .agents/scripts/run-unity-tests.ps1 `
     -TestPlatform EditMode `
     -AssemblyNames Scaffold.Maps.Tests.Performance `
     -PerformanceTestResultsPath "Assets/Packages/com.scaffold.maps/PerformanceTestResults.json"
   ```

   Alternatively invoke Unity.exe yourself with **`-perfTestResults`** (see [Performance testing command-line arguments](https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/manual/cmd-line-args.html)). The Test Runner window often exposes **CSV** only for the results table — use CLI JSON for tooling or transcribe CSV medians into `baselines.json` manually.
8. Optionally feed perf JSON through **[PerformanceBenchmarkReporter](https://github.com/Unity-Technologies/PerformanceBenchmarkReporter)** for HTML/history. Merge chosen medians into `Tests/Performance/baselines.json` **`editor_mono`** (schema still informal — see `$schema_note` and `editor_mono_row_shape_note` in that file).

### IL2CPP lane

Repeat in **Play Mode** with an **IL2CPP** standalone player build when your pipeline supports it. Record separately (e.g. `baselines.json` section or sibling file); canonical allocation profile is IL2CPP per `_benchmarking.md`.

---

## Benchmark suite (Phase 0)

| Test class | Intent |
|------------|--------|
| `MapIndexerValuesBench` | `Indexer.Values` cost — forces materialization via `.Count`; expect large bytes/op proportional to entry count × iterations today. |
| `MapKeyEnumerationBench` | `GetPrimaryKeys` / `GetSecondaryKeys` × 10k on 1000-entry map. **Phase 1:** one `HashSet<>` per call (the `List<>` copy removed). |
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
| _(removed in Phase 1)_ | Half-key `Add(TPrimary\|TSecondary, TValue)` overloads deleted; use **`Add(primary, secondary, value)`** or **`Add(Index<>, value)`**. |

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
