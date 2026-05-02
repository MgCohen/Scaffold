# Implementation plan — `com.scaffold.maps` refactor

**Source:** Audit `com.scaffold.maps.md`, benchmark policy `Docs/Audits/Packages/_benchmarking.md`, and execution review.

**Scope:** All work in-place inside `Assets/Packages/com.scaffold.maps/`; **no consumer migrations** called out by name (see **Consumer-facing deltas** below).

**Branching:** Use your team’s feature branch consistently. If automated agents expect `cursor/<descriptive-name>-e057`, do not parallel a different branch name for the same work.

**Benchmark conventions:** Unity.PerformanceTesting + `Bench.Measure` helper (`_benchmarking.md`), separate performance asmdef, Editor baseline; **IL2CPP PlayMode** when CI or local pipelines support it. Baselines live under `Tests/Performance/baselines.json`; phase-5 comparison output can sit alongside as `Tests/Performance/results-phase5.json`.

---

## Consumer-facing deltas (track explicitly)

These are observable API or behavior contracts; document in README/changelog when shipping.

| Item | Phase | Note |
|------|-------|------|
| `Indexer.Values` return type | 2–3 | Plan narrows from `IReadOnlyCollection<TValue>` to `IEnumerable<TValue>` (zero-alloc projection). May affect overload resolution / consumers that store the property as `IReadOnlyCollection`. `Count` remains on `Indexer`. |
| `GetPrimaryKeys` / `GetSecondaryKeys` ordering | 1 | Returning `HashSet<T>` as `IReadOnlyCollection<T>` removes the `List` copy; **`HashSet` iteration order is undefined** (unlike today’s `new List(keys)` snapshot). Decide: document “unordered”, or preserve deterministic order deliberately. |
| `AddIndexer` duplicate name | 3 | `Dictionary.Add` → explicit check + `InvalidOperationException` (message per audit). |
| `GetIndexedValues` missing name | 3 | `KeyNotFoundException` with explicit message (per audit). |

---

## Phase 0 — Establish baseline (benchmarks + reports)

**Goal:** Capture performance and correctness **before** refactor so Phase 5 can diff.

### 0.1 — Performance harness

- Create `Assets/Packages/com.scaffold.maps/Tests/Performance/` with `Scaffold.Maps.Tests.Performance.asmdef` referencing Unity.PerformanceTesting, NUnit, and Scaffold.Maps (separate from unit-test asmdef).
- If `Bench.cs` does not exist project-wide, add the helper from `_benchmarking.md` (shared `Tests/Performance/Bench.cs` or package `com.scaffold.testing.benchmarks`). Search `Assets/Packages/` first.

### 0.2 — Benchmark suite (align with audit §11)

- **MapIndexerValuesBench.cs** — `Indexer.Values` per-read allocation; maps of 10 / 100 / 1000 entries, all-match indexer; ~10k reads. Expect: **O(N) `List<TValue>` per call** today.
- **MapKeyEnumerationBench.cs** — `GetPrimaryKeys` / `GetSecondaryKeys` × 10k on 1000-entry map. Expect: **HashSet + List** allocated per call today.
- **MapIndexerBulkRebuildBench.cs** — empty map, 5 indexers (10 / 50 / 90% selectivity), bulk `Add` 10 / 100 / 1000 entries.
- **MapVsTupleDictBench.cs** — `Add` / `TryGetValue` / `foreach` for `Map<int,int,string>` vs `Dictionary<(int,int), string>` at 10 / 100 / 1000 entries (documents **`Holder<T>` per `Add`** today).

### 0.3 — Correctness characterization tests (EditMode, not perf)

`Tests/MapIndexerCharacterizationTests.cs` pinning behavior that will change or stay:

- **IndexerValues_DoesNotReflectValueMutation_ByDesign_KeyOnlyPredicates** — audit 4.5 contract.
- **AddIndexer_DuplicateName_ThrowsArgumentException** — today’s `Dictionary.Add` behavior (flip in Phase 3).
- **GetIndexedValues_MissingName_ThrowsKeyNotFoundException** — today’s behavior (flip in Phase 3).
- **Add_HalfKey_TPrimaryRefType_Throws** — audit 4.4 / overloads removed in Phase 1 (replace with “overload absent” assertion or delete).

### 0.4 — Run + record baseline

- Run suite **Editor (Mono)** and **IL2CPP PlayMode** when supported (make IL2CPP availability an explicit Phase 0 checklist item so Phase 5 is not blocked).
- Commit `Tests/Performance/baselines.json`.
- Add `Docs/Audits/Packages/Reports/com.scaffold.maps.baseline.md`: ns/op, bytes/op, Gen0/1/2 per benchmark, characterization-test outcomes.

**Exit criteria:** All benchmarks and characterization tests pass; baseline file and report committed; **no production code changes** yet.

---

## Phase 1 — Easy wins (mechanical cleanup)

Local edits; low design risk. One commit per item or batch trivial items.

- Remove redundant **`predicateIndexers == null`** guards in `Map.cs` and **`data == null`** in `BaseMap.cs` (readonly fields assigned in constructors — audit 4.2).
- Delete **`Holder<T>.EnsureValue`** empty body (audit 4.1); keep **`Holder<T>`** until Phase 2.
- Delete half-key **`Add(TPrimary, TValue)`** and **`Add(TSecondary, TValue)`** (audit 4.4). Update or remove Phase 0 characterization test accordingly.
- **`GetPrimaryKeys` / `GetSecondaryKeys`:** drop **`List<>`** copy; return **`HashSet<>` as `IReadOnlyCollection<>`** (audit 4.8). **Resolve ordering semantics** (see Consumer-facing deltas).
- Delete **`IndexPrimary.cs`** + `.meta` if `Index<TPrimary>` is unreferenced (audit 4.11); verify with grep.
- Remove **`com.scaffold.records`** from `package.json` if unused (audit 4.10); verify with grep.
- **Indexer.cs:** fix brace/formatting (audit 4.3).
- Standardize null checks: **`is null`** for reference types across `Map.cs`, `Indexer.cs`, `BaseMap.cs`, `IndexComposite.cs` (audit 4.3).
- Rename **`Samples/`** → **`Samples~/`** (Unity convention); update `Scaffold.Maps.Samples.asmdef` and any doc paths.
- Delete **`Tests/.gitkeep`** if present (audit 4.15).

**Exit criteria:** All existing tests + Phase 0 perf suite pass; diff mostly deletions/reductions.

---

## Phase 2 — Structural: remove `Holder<T>`, re-key indexers

Audit 5.2 / 7.1 — **main performance and complexity win.**

- **`BaseMap<TKey, TValue>`** wraps **`Dictionary<TKey, TValue>`** directly; remove **`Holder<TValue>`**. Indexer getter/setter and **`IReadOnlyBaseMap`** align with raw values.
- **`Indexer<TP, TS, TV>`** tracks **`HashSet<Index<TP, TS>>`** (not `List<Holder<TValue>>`). Pass a **`Func<Index<TP, TS>, TV>`** value resolver at construction (closure over parent **`TryGetValue`**).
- **`Indexer.Values`** → **`keys.Select(resolveValue)`** as **`IEnumerable<TV>`**; update interface/usage accordingly — **target ~0 bytes/op per read** for repeated enumeration patterns measured in benchmarks.
- **`Map.Add` / `Remove` / `this[index]` set:** call **`Track(Index<...>)` / `Untrack(Index<...>)`** by index key. **`this[index]` value set** still does not re-run predicates (key-only design; Phase 3 documents).
- Delete **`Holder.cs`** + `.meta`.
- Merge **`IndexComposite.cs`** into **`Index.cs`** (audit §2); single file for composite **`Index<>`**.

**Exit criteria:**

- All existing tests pass.
- Phase 0 characterization tests still pass where semantics unchanged.
- Benchmarks show **`Indexer.Values`** allocation drops ~to **~0 bytes/op** (subject to measurement noise).
- **`MapVsTupleDictBench`:** sanity-check **`Map<,,>`** within ~**5%** of tuple-dictionary on **`Add` / `TryGetValue`** after Holder removal.

---

## Phase 3 — Contract clarity

Audit 4.5, 4.9, 4.13, 4.14, 7.4.

- Introduce **`IReadOnlyIndexer<TP, TS, TV>`** (`Name`, `Count`, `Values`). **`Indexer<>`** implements it.
- **`IReadOnlyMap.TryGetIndexer`** returns **`IReadOnlyIndexer<>`** (audit 4.9); concrete **`Map.TryGetIndexer`** may still expose concrete type internally if needed.
- Rename **`predicate`** → **`keyPredicate`**; rename **`AddIndexer`** parameter accordingly; XML docs on **`AddIndexer`** and **`Indexer`**: predicates filter by **key**, not value; **value mutations do not reclassify** entries (audit 4.5).
- **Duplicate indexer name:** replace **`Dictionary.Add`** with explicit check + **`InvalidOperationException`** (`Indexer '{name}' already exists.` — audit 4.13).
- **Missing name in `GetIndexedValues`:** **`TryGetValue`** + **`KeyNotFoundException`** with explicit message (audit 4.14).
- **README:** trim “auto-sync” overstatement; state **key-only predicates**; remove **Scaffold.Records** from allowed deps; align changelog with tests; keep accurate mermaid diagram.
- Flip Phase 0 characterization tests for duplicate-name and missing-name to new exception types/messages.

**Exit criteria:** All tests pass; README matches code.

---

## Phase 4 — Tests for the headline feature

Audit 4.12 / 7.3 — add **`Tests/MapIndexerTests.cs`** (~80 lines, style aligned with **`Samples~/MapIndexerUseCases.cs`**):

- `AddIndexer_RebuildsAgainstExistingEntries`
- `Add_TracksMatchingPredicate` / `Add_DoesNotTrackNonMatching`
- `Remove_UntracksFromIndexer`
- `Clear_ClearsAllIndexers` (membership cleared, not only underlying dictionary)
- `AddIndexer_DuplicateName_Throws` (`InvalidOperationException`)
- `RemoveIndexer_RemovesByName_ReturnsTrueWhenPresent` / `RemoveIndexer_Missing_ReturnsFalse`
- `TryGetIndexer_Missing_ReturnsFalse` / `TryGetIndexer_Present_ReturnsReadOnlyIndexer`
- `GetIndexedValues_MissingName_Throws`
- `IndexerValues_DoesNotReflectValueMutation` (contract test)

**Exit criteria:** File ships; all pass; meaningful indexer surface coverage.

---

## Phase 5 — Re-benchmark + comparison report

**Goal:** Prove delivery against Phase 0 baseline.

- Re-run Phase 0 perf suite (**Editor Mono + IL2CPP** when available).
- Write **`Docs/Audits/Packages/Reports/com.scaffold.maps.refactor-results.md`:** ns/op, bytes/op, Gen0 deltas; **`MapVsTupleDictBench`** sanity paragraph.
- **`results-phase5.json`** (or equivalent) alongside baseline for raw numbers.
- Update **`Tests/Performance/baselines.json`** to post-refactor floor.

**Pass/fail gate** (per `_benchmarking.md`; interpret with engineering judgment):

- **Allocated:** improve or hold at zero where claimed; **no regression > ~10%** without investigation.
- **Gen0:** must not increase materially.
- **Time:** regressions **> ~5%** reviewed; not necessarily auto-fail.
- **`GetAllocatedBytesForCurrentThread`** can jitter; treat borderline failures as “re-run / widen warmup” before changing code.

**Exit criteria:** Comparison report + updated baselines committed; policy thresholds respected; all unit tests green.

---

## Execution checklist (review summary)

1. Confirm **IL2CPP** benchmark lane early (Phase 0 / 5 dependency).
2. Resolve **key enumeration ordering** before or during Phase 1 and document it.
3. Call out **`Indexer.Values`** type change for any public API notes.
4. After **`Samples~/`**, verify **asmdef**, **package layout**, and **README** paths.
5. Use **one consistent git branch name** for the full effort.
