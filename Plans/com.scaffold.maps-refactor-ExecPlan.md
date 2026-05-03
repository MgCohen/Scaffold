# Implementation plan — `com.scaffold.maps` refactor

**Package:** [`Assets/Packages/com.scaffold.maps/`](../Assets/Packages/com.scaffold.maps/)  
**Goals:** Perf + correctness baseline; mechanical cleanup; remove `Holder<T>`; clearer contracts; indexer test coverage; re-benchmark and document results.  
**Constraint:** Work in-place; **no consumer migrations** required (audit found zero external `AddIndexer` callers).

Benchmark conventions follow [`Docs/Audits/Packages/_benchmarking.md`](../Docs/Audits/Packages/_benchmarking.md) — Unity.PerformanceTesting + `Bench.Measure`, Editor + IL2CPP lanes where supported, [`Assets/Benchmarks/Maps/baselines.json`](../Assets/Benchmarks/Maps/baselines.json). The perf suite was relocated out of the package tree; see the migration note below.

---

## Phase 0 — Establish baseline (benchmarks + reports)

**Goal:** Capture the current perf/correctness profile before any refactor, so Phase 5 has numbers to diff against.

### 0.1 — Perf harness

- `Assets/Benchmarks/Maps/` + `Scaffold.Benchmarks.Maps.asmdef` (references `Scaffold.Benchmarks`, `Scaffold.Maps`, `Unity.PerformanceTesting`, `TestAssemblies`). Phase 0 originally placed this under `Assets/Packages/com.scaffold.maps/Tests/Performance/`; it was relocated outside the package tree so the UPM surface stays lean.
- `Bench.cs`: per-package (no shared `com.scaffold.testing.benchmarks` in this repo) — reuse pattern from `_benchmarking.md`.

### 0.2 — Benchmark suite (audit §11)

| File | Scenario |
|------|-----------|
| `MapIndexerValuesBench.cs` | `Indexer.Values` / materialization pressure; historically `List<T>` per read. |
| `MapKeyEnumerationBench.cs` | `GetPrimaryKeys` / `GetSecondaryKeys` × 10k on 1000-entry map. |
| `MapIndexerBulkRebuildBench.cs` | Empty map + 5 indexers (10/50/90% selectivity) + bulk Add 10 / 100 / 1000. |
| `MapVsTupleDictBench.cs` | `Map<int,int,string>` vs `Dictionary<(int,int),string>` — Add / TryGetValue / foreach. |

### 0.3 — Characterization tests (EditMode)

- Originally `Tests/MapIndexerCharacterizationTests.cs` — pinned behavior later changed Phases 1 & 3.  
- **Status:** superseded by `Tests/MapIndexerTests.cs` after refactor landed.

### 0.4 — Run + record baseline

- Editor (Mono); IL2CPP when CI supports it.
- Commit `baselines.json` + maintain [`Docs/Audits/Packages/Reports/com.scaffold.maps.baseline.md`](../Docs/Audits/Packages/Reports/com.scaffold.maps.baseline.md).

### Exit criteria

- Benchmarks + unit tests green; baseline file recorded. No production refactor yet.

---

## Phase 1 — Easy wins (mechanical cleanup)

| # | Task | Audit |
|---|------|--------|
| 1 | Delete redundant `predicateIndexers == null` / `data == null` guards (`Map`, `BaseMap`) | 4.2 |
| 2 | Delete `Holder<T>.EnsureValue` (empty) | 4.1 |
| 3 | Remove half-key `Add(TPrimary|TSecondary, TValue)`; update/remove characterization covering them | 4.4 |
| 4 | `GetPrimaryKeys` / `GetSecondaryKeys` — return `HashSet` as `IReadOnlyCollection` (no extra `List` copy) | 4.8 |
| 5 | Delete `IndexPrimary.cs` (+ meta) — unused `Index<TPrimary>` | 4.11 |
| 6 | Remove `com.scaffold.records` from `package.json` if unused | 4.10 |
| 7 | Reformat `Indexer.cs` | 4.3 |
| 8 | Standardize `is null` for reference predicates / args | 4.3 |
| 9 | Rename `Samples/` → `Samples~/` (+ asmdef); Unity embed convention | theme #12 |
| 10 | Delete `Tests/.gitkeep` if present | 4.15 |

### Exit criteria

- Existing + perf assemblies pass; diff mostly mechanical.

---

## Phase 2 — Structural: remove `Holder<T>`, key-track indexers

Audit **5.2 / 7.1**.

| Step | Detail |
|------|--------|
| 1 | `BaseMap<TKey,TValue>` wraps `Dictionary<TKey, TValue>` directly. |
| 2 | `Indexer` tracks `HashSet<Index<TP, TS>>`; resolve values via owning `Map.TryGetValue`. |
| 3 | `Values` exposes a projection type (implemented: **`IndexerValuesView`** — struct enumerator, **`Count`** O(1)), not a per-read `List<T>`. |
| 4 | `Add` / `Remove` / `[index] =` paths: **`Track` / `Untrack` by index**; **[index] set still does not re-run predicate** (key-only design — Phase 3 documents). |
| 5 | Delete `Holder.cs` + meta. |
| 6 | Merge composite index into **`Index.cs`** (rename from `IndexComposite`). |

### Exit criteria

- All tests pass; **`Indexer.Values.Count`** hot path allocation-free (`PerformanceAllocationVerificationTests`).

---

## Phase 3 — Contract clarity

Audit **4.5, 4.9, 4.13, 4.14, 7.4**.

| # | Task |
|---|------|
| 1 | `IReadOnlyIndexer<TP,TS,TV>` — `Name`, `Count`, `Values` (**`IndexerValuesView`** implements read surface). |
| 2 | `IReadOnlyMap.TryGetIndexer` → **`out IReadOnlyIndexer<>`**; `AddIndexer(..., keyPredicate)` naming + XML: *predicates filter by key, not value*. |
| 3 | Duplicate name → **`InvalidOperationException("`Indexer '{name}' already exists.`")`** (not bare `Dictionary.Add`). |
| 4 | Missing `GetIndexedValues` → **`KeyNotFoundException`** with *Indexer '…' not registered. Call AddIndexer first.* |
| 5 | README — key-only line, trim stale Records/changelogclaims; keep mermaid. |
| 6 | Flip/supersede old characterization assertions in **`MapIndexerTests`**. |

### Exit criteria

- Tests green; README matches code.

---

## Phase 4 — Tests for the headline feature

Audit **4.12 / 7.3** — **`Tests/MapIndexerTests.cs`**

Target scenarios (names may vary slightly in repo):

- `AddIndexer_RebuildsAgainstExistingEntries`
- `Add_TracksMatchingPredicate` / `Add_DoesNotTrackNonMatching`
- `Remove_UntracksFromIndexer`
- `Clear_ClearsAllIndexers`
- `AddIndexer_DuplicateName_ThrowsInvalidOperationException`
- `RemoveIndexer_*` present/missing
- `TryGetIndexer_*` + read-only surface
- `GetIndexedValues_MissingName_*`
- Value mutation membership contract

### Exit criteria

- Meaningful indexer surface coverage.

---

## Phase 5 — Re-benchmark + comparison report

| Step | Artifact |
|------|-----------|
| 1 | Re-run Phase 0 perf suite (Editor Mono + IL2CPP if available). |
| 2 | Optional: **`Assets/Benchmarks/Maps/results-phase5.json`** alongside baseline. |
| 3 | [`Docs/Audits/Packages/Reports/com.scaffold.maps.refactor-results.md`](../Docs/Audits/Packages/Reports/com.scaffold.maps.refactor-results.md) — ns/op, bytes/op, AllocCount, Gen deltas vs baseline; **`MapVsTupleDictBench`** sanity. |
| 4 | Update **`baselines.json`** post-refactor medians (**remove** stale notes once refreshed). |

### Gates (`_benchmarking.md`)

- **Allocated:** improve or flat; avoid >10% regression where counter is trustworthy.
- **Gen0:** must not increase.
- **Time:** >5% regression → review, not necessarily fail.

---

## Risks / decisions (logged)

| Topic | Resolution in this effort |
|--------|----------------------------|
| `Bench.cs` location | Originally inline under `Tests/Performance/Bench.cs` in the maps package; lifted to a repo-internal canonical at `Assets/Benchmarks/Bench/Bench.cs` (asmdef `Scaffold.Benchmarks`) when authoring the states refactor Phase 0, so future package benchmarks can reference it instead of copying. |
| `TryGetIndexer` break | Accepted — zero repo callers outside package. |
| `Indexer.Values` type | **`IndexerValuesView`** — **`IReadOnlyCollection<T>`**, O(1) **`Count`**, struct enumerator avoids list-per-read while keeping countable API. Generic interface uses implementation type for **`Values`** to avoid ambiguity. |
| IL2CPP / CI | Baseline/report docs allow Editor-only capture when IL2CPP lane unavailable; note in refactor results. |

---

## Completion snapshot (iteration anchor)

_Use this subsection to tick items as you reconcile branches, CI, and baselines._

| Phase | Implementation | Follow-up |
|-------|----------------|-----------|
| 0 | Harness + benchmarks + initial `baselines.json` captured (2026-05-02 row set) | Mark stale after Phase 2+; refresh medians |
| 1 | Done on Phase 1 branch / merged into refactor branch | — |
| 2–5 | Done on **`cursor/maps-phases2-5-refactor-39f9`** — PR **#29** vs base **`cursor/maps-phase0-perf-e057`** | **Unity:** rerun perf → replace `editor_mono` in `baselines.json`; optionally fill `results-phase5.json` |
| Docs | [`com.scaffold.maps.refactor-results.md`](../Docs/Audits/Packages/Reports/com.scaffold.maps.refactor-results.md), updated [`com.scaffold.maps.baseline.md`](../Docs/Audits/Packages/Reports/com.scaffold.maps.baseline.md) | Paste real median table when exported |

---

## How to iterate

1. Edit **this file** — add phases, revise exit criteria, or link new PRs.  
2. Keep **audit canonical detail** in [`Docs/Audits/Packages/com.scaffold.maps.md`](../Docs/Audits/Packages/com.scaffold.maps.md); this ExecPlan stays the **checkpoint / checklist**.  
3. After each perf-changing change: update **`baselines.json`** and the **completion snapshot** row above.

---

## References

- [`Docs/Audits/Packages/com.scaffold.maps.md`](../Docs/Audits/Packages/com.scaffold.maps.md) — full audit  
- [`Assets/Packages/com.scaffold.maps/README.md`](../Assets/Packages/com.scaffold.maps/README.md) — runtime contract  
- [`Docs/Tools/Maps.md`](../Docs/Tools/Maps.md) → README shortcut
