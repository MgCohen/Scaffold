# Implementation plan — `com.scaffold.states` refactor

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. Maintain it in accordance with [`PLANS.md`](../PLANS.md).

**Package:** [`Assets/Packages/com.scaffold.states/`](../Assets/Packages/com.scaffold.states/)
**Audit:** [`Docs/Audits/Packages/com.scaffold.states.md`](../Docs/Audits/Packages/com.scaffold.states.md) (sections referenced as §x.y below)
**Project audit context:** [`Docs/Audits/Packages/_index.md`](../Docs/Audits/Packages/_index.md) — themes 1, 3, 4, 6, 8, 10
**Companion (entities.states):** [`Docs/Audits/Packages/com.scaffold.entities.states.md`](../Docs/Audits/Packages/com.scaffold.entities.states.md). Several findings co-tax this plan; cross-package fixes (e.g. typed-key `Subscribe`) land here, not there.

Benchmark conventions follow [`Docs/Audits/Packages/_benchmarking.md`](../Docs/Audits/Packages/_benchmarking.md) — Unity.PerformanceTesting + the canonical `Scaffold.Benchmarks` `Bench.Measure` helper at `Assets/Benchmarks/Bench/Bench.cs`, Editor + IL2CPP lanes where supported. Per-package benchmarks and their `baselines.json` live under `Assets/Benchmarks/<PackageShortName>/` (states: [`Assets/Benchmarks/States/baselines.json`](../Assets/Benchmarks/States/baselines.json)) — outside the UPM tree so the package surface stays lean.

## Purpose / Big Picture

After this refactor, a developer working on `com.scaffold.states` (and on its single external consumer, `com.scaffold.entities.states`) gains:

- A `Store` whose hot paths (`Execute<TPayload>`, `EnumerateAll<TState>`, `NotifyReferenceSubscriptions`) are reentrancy-safe and zero-alloc on the steady-state path. Today, all three use shared instance buffers or per-call iterator/list allocations and corrupt iteration if a subscriber re-enters the store.
- A fail-fast contract on every public API. `Snapshot.Get<TState>` and `Store.Execute<TPayload>` no longer hide missing keys / unregistered payloads behind a `null` return or `Debug.LogWarning`; both throw typed exceptions consumers can react to.
- A pure-C# runtime. The single `UnityEngine.Debug.LogWarning` in `Store.cs` is removed and the asmdef flips to `noEngineReferences: true`, matching the package's stated boundary.
- A typed slice-key contract. `IReference` becomes a discriminated abstraction (`abstract record Reference` or analyzer-enforced equality contract), so `EntityStateReference` and any future key type cannot silently mis-hash inside the slice map.
- Source-generated payload→mutator dispatch. `EntityBridgeContext.RegisterMutators` (six hand-edited `RegisterMutator` calls today) and the runtime `Dictionary<Type, List<…>>` lookup both disappear in favor of a `[Mutator]`-attribute discovery generator under `Generators/`.
- A VContainer `Container/StatesInstaller`, matching the project convention (`AGENTS.MD`).
- Concrete benchmark numbers proving each claim, recorded in `Assets/Benchmarks/States/baselines.json` and a sibling refactor-results report.

The change is observable: the existing test suite (`Assets/Packages/com.scaffold.states/Tests/`) goes from green-with-known-smells to green-against-stricter-contracts; new benchmarks and regression tests under `Assets/Benchmarks/States/` actively prove the bugs the audit calls out are no longer reachable.

**Constraint:** in-place; the only external consumer is `com.scaffold.entities.states`. Migrate that package within the same plan when an API breaks. Audit verified zero other consumers under `Assets/`, `GameModule/`, `LiveOps/`.

## Progress

- [x] Phase 0 — Establish baseline (perf harness + benchmarks + `baselines.json`)
- [x] Phase 1 — Easy wins (audit §6 + companion §4 line-items: Unity leak, fail-fast, guard-clause de-dup, `ReferenceEquals`, `TRef` constraint, duplicate canonical detection)
- [x] Phase 2 — Reentrancy & buffer hygiene (§4.5, §4.6, §4.9, §4.11)
- [x] Phase 3 — Indexed slice store (§7.3) + `EnumerateAll` struct enumerator (§4.6 perf)
- [x] Phase 4 — Contract clarity (§4.15 `IReference`, §4.18 aggregate lifetime, §4.19 scratchpad reset, §4.20 `[NotNullWhen]`, §3.7 `Snapshot` composes-not-inherits, callback overload narrowing, §4.14 fat interface)
- [x] Phase 5 — `IMutatorDispatcher` abstraction in the runtime + hand-maintained per-consumer `GeneratedMutatorDispatcher` (Roslyn generator optional follow-up; §7.1, theme 1, theme 2)
- [x] Phase 6 — VContainer `Runtime/Container/StatesInstaller` (§7.6, theme 8) + `Samples~/` + `package.json`; README/XML polish deferred
- [x] Phase 7 — Comparison report scaffold + `baselines.phase0.json` archive; full re-benchmark still required locally (`results-phase7.json` + updated `baselines.json`)

## Surprises & Discoveries

- Observation: Custom Roslyn ordering rules (`SCA3003`) require `Store` members in declaration order **constructor → properties → fields → methods**; instance helpers on benchmark fixtures must precede `static` helpers.
  Evidence: Analyzer passes on `Scaffold.States` runtime after reordering `Store` and `StoreEnumerateAllReentrancyTests`.

## Decision Log

- Decision: Keep the `Mutator<TState>` and `Mutator<TState, TPayload>` two-shot generic abstraction unchanged.
  Rationale: Audit §3 explicitly calls this out as the right shape. No consumer pain on this surface; the win in Phase 5 is replacing the *registry* and *runtime dispatch*, not the mutator type itself.
  Author: ExecPlan author.
- Decision: The single external consumer (`com.scaffold.entities.states`) is migrated inside this plan, not in a follow-up.
  Rationale: Audit §Consumers documents that ~120 lines of duplication live there because of states-API friction. Doing the migration in-band keeps the contract changes honest — every breaking change is observed in the consumer in the same PR.
  Author: ExecPlan author.
- Decision: Phase 5 (source-gen dispatcher) is gated on Phases 0–4 landing first.
  Rationale: It is the largest investment (1–2 days, audit §7.1), and Phases 1–4 already remove enough boxing and guard noise that the source-gen win can be measured cleanly against a stable baseline. Doing it before §4.5 / §4.11 fixes would mix two refactors and confuse the benchmark deltas.
  Author: ExecPlan author.
- Decision: The runtime declares `IMutatorDispatcher`; the generator emits a sealed `GeneratedMutatorDispatcher : IMutatorDispatcher` into each consumer assembly. The earlier draft proposed a `partial class MutatorDispatcher` shared between runtime and consumer assemblies — that doesn't compile because C# `partial` declarations only merge within a single compilation unit. The interface is the cross-assembly seam; the generator owns the consumer-assembly impl.
  Rationale: A consumer-assembly-only generated class can't complete a `partial` declared in the runtime — they're different compilation units. An interface (or delegate) is the only way the runtime `Store` can depend on a type whose implementation lives in a different assembly. `TryDispatch`-returning-`bool` lets the dispatcher cleanly defer to the runtime `MutatorRegistry` slow-path when a payload type wasn't `[Mutator]`-decorated.
  Author: ExecPlan author.
- Decision: Phase 0 adds a `readonly record struct` payload baseline (`Execute_ValuePayload_OneSlice`) alongside the record-class baseline.
  Rationale: Phase 5's "≥3× ns/op for value-type payloads" acceptance needs a like-for-like Phase 0 number. The record-class baseline already pays only one cast (no box), so the source-gen win there is small; the value-type baseline pays a guaranteed box and is where the §7.1 win lands hardest. Without both, the ≥3× target is non-comparable.
  Author: ExecPlan author.
- Decision: Benchmarks define their own minimal fixtures (`Assets/Benchmarks/States/BenchFixtures.cs`) instead of referencing `Scaffold.States.Samples`.
  Rationale: Phase 6 renames `Samples/` to `Samples~/` per Unity convention, which makes the samples assembly invisible to the project. A benchmark suite that depended on it would silently break at that point. Self-contained fixtures keep Phase 0 stable across the entire refactor.
  Author: ExecPlan author.
- Decision: `IReference` becomes an `abstract record Reference` rather than an interface + analyzer.
  Rationale: Records give value-equality for free, eliminate the `Scratchpad.ReferenceByValueEqualityComparer` workaround (§4.15), and concretely solve the equality footgun. The analyzer route remains as a fallback if breaking the interface is too costly for an unanticipated consumer; resolved in Phase 4 once the consumer migration is in flight.
  Author: ExecPlan author.

## Outcomes & Retrospective

To be filled at completion of each phase and a final summary at the end of Phase 7.

**Phase 2 (partial note):** Reentrancy-safe slice enumeration uses pooled `List<>` buffers per `GetAll`/`EnumerateAll`/`FillSlices`; notify paths snapshot subscriptions before invoking; deferred flush uses ping-pong lists instead of `ToArray()`; `Scratchpad.Reset` clears overlay, slice buffer, and ref-set; benchmarks `StoreEnumerateAllReentrancyTests` and `StateEventHandlerInlineUnsubscribeTests` enabled; added `Notify_50Subs_HalfUnsubscribeInline` (+ `NoAllocations`), deferred flush GC regression, and pooled mutator `GetAll` count test.

**Phase 3 (indexed enumeration):** `Store` maintains `slicesByStateType` updated on slice registration/removal and ctor seeding; `FillSlices` walks that bucket (no `Map.GetAll` scan); hot enumeration uses `EnumerateAllPairs<TState>()` → `EnumerateAllPairsResult<TState>` with a pooled `Enumerator` implementing `IDisposable`; `StoreEnumerationExtensions.EnumerateAll` keeps IEnumerable/for-each call sites working.

**Phase 4 (partial — reference key):** Removed marker `IReference`; public key type is `abstract record Reference` with nested sealed singleton `Reference.NullReference`; `EntityStateReference` and all former `IReference` usages now use `Reference`. Scratchpad uses default `HashSet<Reference>` equality (record/value semantics). Remaining Phase 4 items: subscribe narrowing, `IAggregateProvider.Wire` → `IDisposable` + `UnregisterAggregate`, `Snapshot` composition, `IStateEventHandler` slim-down + extensions, XML/README pass.

## Context and Orientation

`com.scaffold.states` lives at `Assets/Packages/com.scaffold.states/` and ships a Redux-flavored slice/store: a `Store` indexes `Slice` (canonical, mutable) and `AggregateSlice` (derived, read-only) instances by `(Reference reference, Type stateType)`, applies `Mutator<TState>` / `Mutator<TState, TPayload>` updates, fans events through `IStateEventHandler`, and supports `SaveSnapshot` / `LoadSnapshot`. The runtime asmdef references `Scaffold.Records`, `Scaffold.Maps`, `Scaffold.Pooling` and is currently `noEngineReferences: false` because of one `UnityEngine.Debug.LogWarning` call (`Runtime/Store.cs:264`).

Key files:

- `Runtime/Store.cs` — the central API surface. `Execute*`, `RegisterSlice/UnregisterSlice`, `RegisterAggregate`, `Save/LoadSnapshot`, `Get/GetAll/EnumerateAll`, plus a private `Scratchpad : IStoreScratchpad` used by `MutatorRunner`.
- `Runtime/Pipeline/MutatorRegistry.cs`, `RegisteredMutator.cs`, `MutatorRunner.cs`, `IPayloadMutatorBinding.cs`, `IStoreScratchpad.cs`, `DuplicateMutatorRegistrationException.cs` — the dispatch internals.
- `Runtime/Events/StateEventHandler.cs`, `Ledger.cs`, `TypedSubscription.cs`, `DeferredStateEventHandler.cs`, `StateEventHandlers.cs`, `StateEventMergeMode.cs` — the subscription / notification path.
- `Runtime/State/State.cs`, `BaseState.cs`, `AggregateState.cs`, `Slice.cs`, `BaseSlice.cs`, `AggregateSlice.cs`, `AggregateProvider.cs`, `IAggregateProvider.cs`, `IAggregateRebuild.cs`, `Snapshot.cs`, `StateChangeEvent.cs` — domain types.
- `Runtime/Builders/Store/StoreBuilder.cs`, `StoreBuilderMethods.cs`, `Builders/State/StateBuilder.cs`, `GenericStateBuilder.cs` — composition helpers.
- `Runtime/Abstractions/Reference.cs` — abstract slice-key record (`Reference.Null`) and nested `NullReference` singleton.
- `Runtime/Abstractions/IPayloadReference.cs`, `IStateScope.cs`, `IStoreScope.cs`, `ISubscription.cs`, `IStateEventHandler.cs`, `IStateEventDeferralController.cs` — public abstractions.
- `Tests/` — seven EditMode test files (~770 LOC). Cover keyed canonical aggregate, deferred merge modes, dedup on registry, batch pool poisoning, scratchpad-`GetAll` regression.
- `Samples/` — Counter/Notes/Totals demo (`SampleStoreFactory`, `SampleStates`, `SamplePayloads`, `SampleMutators`, `SampleReference`, `TotalsAggregateProvider`).

External consumer (single):

- `Assets/Packages/com.scaffold.entities.states/` — `EntityStateReference`, `StoreInstanceIdExtensions` (extension methods that wrap `Store.X(Reference, …)` as `Store.X(InstanceId, …)`), `StoreVariableStorage`, `StateEntity`, `StateEntityOps`, `EntityBridgeContext` (six hand-rolled `RegisterMutator` calls), and the entity-side mutators / payloads.

Conventions (from `AGENTS.MD` and `_index.md`):

- DI: VContainer per-package `Container/Installer`. States ships none today (theme 8).
- Source generators live under `Generators/` at repo root. Existing examples: `MVVMCompositionGenerator`, `LiveOpsKeyGenerator`. Phase 5 adds `Scaffold.States.MutatorDispatcherGenerator` alongside them; the generator emits per-consumer-assembly `GeneratedMutatorDispatcher : IMutatorDispatcher` classes and the runtime owns the `IMutatorDispatcher` interface.
- Pure-C# packages set `noEngineReferences: true`. States is currently `false` because of one Unity log.

Term-of-art definitions used in this plan:

- **Canonical slice** — a `Slice` row keyed by `(IReference, Type)`; mutated by `Mutator<TState>` and persisted by `SaveSnapshot`.
- **Aggregate slice** — an `AggregateSlice` whose `State` is rebuilt by an `IAggregateProvider` from canonical slices; never participates in `SaveSnapshot`.
- **Overlay** — the per-`Execute*` `Snapshot` instance held on the scratchpad. Mutators read pending values from the overlay first, fall back to the live store; `CommitOverlay` applies the overlay back to the store and notifies subscribers.
- **Reentrancy hazard** — a subscriber callback fired during `Notify` re-enters the store (e.g. calls `GetAll<TState>` or `Subscribe`) and corrupts iteration of a shared instance buffer. Audit §4.5, §4.11 document the concrete hazards.
- **Bench.Measure** — the per-package helper documented in `Docs/Audits/Packages/_benchmarking.md`. Reports six sample groups (`Time`, `Allocated`, `AllocCount`, `Gen0`, `Gen1`, `Gen2`).

## Plan of Work

The plan is sequenced as seven phases. Each phase has a clear exit criterion. Phases 0–2 are mechanical and low-risk; Phase 3 introduces a secondary index data structure; Phase 4 changes a public abstraction (`IReference`) and migrates the one consumer; Phase 5 introduces a Roslyn source generator; Phase 6 ships the DI installer and docs; Phase 7 re-benchmarks and writes the comparison report.

### Phase 0 — Establish baseline (benchmarks + reports)

Goal: capture the current perf/correctness profile before any refactor, so Phase 7 has numbers to diff against. Mirrors the maps Phase 0 setup (`Plans/com.scaffold.maps-refactor-ExecPlan.md` Phase 0).

**0.1 Perf harness.** Per the project-wide convention captured in `Docs/Audits/Packages/_benchmarking.md`, perf tests live **outside** the package tree under `Assets/Benchmarks/`. The canonical `Bench.Measure` lives at `Assets/Benchmarks/Bench/Bench.cs` (asmdef `Scaffold.Benchmarks`) and is referenced — not copied — by each per-package perf asmdef. For states the layout is:

- `Assets/Benchmarks/States/Scaffold.Benchmarks.States.asmdef` — references `Scaffold.Benchmarks`, `Scaffold.States`, `Scaffold.Pooling`, `Unity.PerformanceTesting`. `includePlatforms: [Editor]`. Deliberately does **not** reference `Scaffold.States.Samples`: Phase 6 will rename `Samples/` to `Samples~/` (Unity convention — tilde-suffixed folders are imported via Package Manager only and don't compile into the project), and a benchmark assembly that depended on the samples assembly would silently break at that point.
- `Assets/Benchmarks/States/BenchFixtures.cs` — minimal self-contained types (`CounterState`, `SampleKey`, `CombinedTickPayload`, `ValueCombinedTickPayload`, `ApplyCombinedTickToCounter`, `ApplyValueCombinedTickToCounter`) so the benchmark suite has no dependency on the samples assembly.
- `Assets/Benchmarks/States/StatesBenchmarksAssemblySetup.cs` — per-assembly `[SetUpFixture]` calling the shared `Scaffold.Benchmarks.BenchSetup` (which save/restores `LogAssert.ignoreFailingMessages`).
- `Scaffold.States/Runtime/AssemblyInfo.cs` adds `[assembly: InternalsVisibleTo("Scaffold.Benchmarks.States")]` so internal-surface micro-benchmarks (e.g. `MutatorRegistry.TryGet`) can be measured directly without going through the Pool overhead of `Execute<>`.

**0.2 Benchmark suite.** One file per scenario, all using `Scaffold.Benchmarks.Bench.Measure(...)`. The audit `## Benchmark plan` enumerates the canonical set; bind each entry to a file under `Assets/Benchmarks/States/`:

- `StoreExecuteBenchmarks.cs::Execute_SingleMutator_OneSlice` — current path with reference-type (`record class`) payload (audit baseline). Sibling `Execute_TypedMutator_OneSlice_NoRegistry` measures the `ExecuteMutator(mutator, payload)` floor that skips `MutatorRegistry`.
- `StoreExecuteBenchmarks.cs::Execute_ValuePayload_OneSlice` — same path but with a `readonly record struct` payload. Today `Execute<TPayload>(TPayload)` boxes value-type payloads at the `(object)payload` boundary inside `RunRegisteredMutatorsWithoutCommit`; this benchmark anchors Phase 5's "≥3× ns/op for value-type payloads" target to a like-for-like Phase 0 number. Sibling `Execute_TypedMutator_ValuePayload_OneSlice_NoRegistry` is the value-type `ExecuteMutator` floor.
- `StoreEnumerateAllBenchmarks.cs::{EnumerateAll,GetAll}_OneTypeBucket_1k` — 1k slices of one `TState`, 100 iterations per measurement.
- `NotifyBenchmarks.cs::Notify_50Subs_NoMutation` — 50 subscribers, non-mutating fanout cost. The inline-unsubscribe variant (`Notify_50Subs_HalfUnsubscribeInline`) cannot run pre-Phase-2 (the §4.11 throw kills it) — Phase 2 introduces it as a perf test alongside un-ignoring the regression test.
- `MutatorRegistryBenchmarks.cs::TryGet_{Known,Unknown}Key_50TypesRegistered` — registry preloaded with 50 distinct payload types.
- `ReferenceEqualityBenchmarks.cs::{Equals_VirtualDispatch,ReferenceEquals}_VsReferenceNull` — mixed `Reference.Null` and a deliberately-misbehaving `IReference.Equals` override; demonstrates the §4.13 cliff.
- `SubscribeBenchmarks.cs::Subscribe_PerCall_CachedDelegate` — cached delegate so the unavoidable closure alloc is outside the measurement and the harness sees only `Ledger.Add` overhead.
- `StoreEnumerateAllReentrancyTests.cs` (regular `[Test]`, not `[Performance]`) — proves the §4.5 shared-buffer corruption today: a subscriber that calls `GetAll<CounterState>` from inside `Notify` while the outer caller iterates `EnumerateAll<CounterState>` corrupts the iteration. Marked `[Ignore("Expected red until com.scaffold.states-refactor Phase 2 lands.")]` so CI stays green; un-ignore in Phase 2.
- `StateEventHandlerInlineUnsubscribeTests.cs` — proves the §4.11 `InvalidOperationException: Collection was modified` today (a subscriber unsubscribes itself from inside `Notify`). Same `[Ignore]` strategy.

**0.3 Run + record baseline.** Editor (Mono) with `-perfTestResults Assets/Benchmarks/States/baselines.json`; IL2CPP if CI supports it. Maintain a baseline-narrative doc at [`Docs/Audits/Packages/Reports/com.scaffold.states.baseline.md`](../Docs/Audits/Packages/Reports/com.scaffold.states.baseline.md) summarizing the medians and noting any byte-counter availability quirks (`Bench.ByteSource`).

Exit criteria: perf assembly compiles, all `[Performance]` tests run green, the two reentrancy/inline-unsubscribe regressions are present but `[Ignore]`-tagged so CI does not block, and a real `baselines.json` (replacing the placeholder) is committed.

### Phase 1 — Easy wins (mechanical cleanup)

Each item below is <30 min and individually compileable. Group by file when convenient; commit per logical step. Ties to audit §6 + the `4.x` line items it points at.

| # | File / change | Audit |
|---|---|---|
| 1 | Delete `UnityEngine.Debug.LogWarning` at `Store.cs:264`; throw a new `MutatorNotRegisteredException(Type payloadType)` from `Runtime/Pipeline/`. Flip `Scaffold.States.asmdef` `noEngineReferences: true`. | §4.1, §4.2, §6.1, theme 6 |
| 2 | `Snapshot.Get<TState>(IReference)` (`Snapshot.cs:20–27`) throws `KeyNotFoundException` on miss; add sibling `bool TryGet<TState>(IReference, out TState)`. | §4.8, §6.2, theme 4 |
| 3 | De-duplicate null-checks: keep checks on public `Store.*` entry points; remove from `MutatorRegistry.Register` (`Pipeline/MutatorRegistry.cs:13`), `StateEventHandler.Subscribe/Unsubscribe/SubscribeAny` (`Events/StateEventHandler.cs:65, 75, 85, 113`), `DeferredStateEventHandler.Notify`'s inner overload (`:29`), and the per-element check inside `ApplyOnePayloadToOverlay` (`Store.cs:251`). | §4.3, §6.3, theme 3 |
| 4 | Replace `ContainsKey + indexer` with `TryGetValue` in `Ledger.Get` (`Ledger.cs:11`), `Ledger.Add` (`:22`), and `StateEventHandler.NotifyReferenceSubscriptions` (`StateEventHandler.cs:28, 33`) / `AddReferenceSubscription` (`:123`). | §4.10, §6.4 |
| 5 | `MutatorRunner.RunMutatorBindingsWithoutCommit` switch `foreach` → `for (int i = 0; i < mutators.Count; i++)` (`Pipeline/MutatorRunner.cs:34`). | §4.12, §6.5 |
| 6 | `RegisteredMutator.Apply` swap `executeReference.Equals(Reference.Null)` for `ReferenceEquals(executeReference, Reference.Null)` (`Pipeline/RegisteredMutator.cs:29`). | §4.13, §6.8 |
| 7 | `StateBuilder<TRef, TState>` (`Builders/State/StateBuilder.cs:3`) add `where TRef : IReference`. | §4.16, §6.7 |
| 8 | `StoreBuilder.AddState` (`Builders/Store/StoreBuilder.cs:74`) detect duplicate `(reference, state-type)` pairs symmetrically with the aggregate path at `:58`. Add a `HashSet<(IReference, Type)> registeredCanonical` mirror. | §4.17, §6.6 |
| 9 | `MutatorRegistry.TryGet` annotate `out` with `[NotNullWhen(true)] out IReadOnlyList<IPayloadMutatorBinding>? bindings` and drop the redundant `bindings == null` branch in `Store.RunRegisteredMutatorsWithoutCommit`. | §4.20 |
| 10 | Move `Runtime/Utility/Reference.cs` next to `IReference.cs` under `Runtime/Abstractions/`. Cosmetic, but it closes the docs gap noted in §8. | §8 |
| 11 | Delete redundant `?? Reference.Null` ladder by introducing one private static `Resolve(IReference?)` helper on `Store` and inlining at all eight call sites. | §5.5 |
| 12 | `StateEventHandlers` (factory plural) rename to `StateEventHandlerFactory` (singular). Update three internal callers + the two README examples. | §8 |

Exit criteria: existing test suite + Phase 0 perf suite green; the §6 `noEngineReferences: true` flip causes no analyzer or IL2CPP failure; `git grep "UnityEngine" Assets/Packages/com.scaffold.states/Runtime` returns zero hits.

### Phase 2 — Reentrancy & buffer hygiene

Goal: the two regression tests left red in Phase 0.3 turn green; nothing else regresses. Targets §4.5, §4.6, §4.9, §4.11.

**2.1 `Store` shared instance buffers (§4.5).** Replace `Store.mapSliceBuffer`, `aggregateSliceBuffer`, `sliceBuffer`, `pruneBuffer` (lines 45–48) with rented buffers per call. Two acceptable patterns:

- A `Pool<List<T>>` per slice-buffer kind, scoped to each method that needs one. Same `Scaffold.Pooling.Pool<>` already used for `MutatorRunner`. Returned in a `try/finally`.
- Or per-method local `List<T>` allocations. Acceptable for cold paths; not acceptable for `EnumerateAll`/`GetAll` which the audit benchmark plan calls out as hot.

Decision: use the pool for `mapSliceBuffer`, `aggregateSliceBuffer`, `sliceBuffer`; per-method local `List<>` for `pruneBuffer` (only used by `LoadSnapshot`, which is cold).

**2.2 `EnumerateAll<TState>` reentrancy (§4.6).** The current iterator yields while indexing into `sliceBuffer`. Two interleaved fixes:

- Stop using a shared buffer (resolved by §2.1).
- Materialize the list to a *local* before yielding so a re-entrant subscriber call cannot mutate the iteration target.

Phase 3's secondary index will further drop allocations; Phase 2 only fixes the correctness bug.

**2.3 `StateEventHandler.NotifyReferenceSubscriptions` snapshot-then-iterate (§4.11).** Take a defensive copy of the `List<ISubscription>` for the (reference, stateType) bucket before invoking; the copy buffer is rented from a `Pool<List<ISubscription>>`. Same treatment for `NotifyTypeWideSubscriptions` and `NotifyAnySubscriptions`. The `InvalidOperationException` regression test from Phase 0 turns green. Once the path no longer throws, add a `NotifyBenchmarks.cs::Notify_50Subs_HalfUnsubscribeInline` `[Test, Performance]` (50 subscribers, half `Unsubscribe` themselves on the first notify) and a companion `Bench.NoAllocations` so the Phase 2 / Phase 7 zero-allocation gate is validated against the actual inline-unsubscribe workload, not just the non-mutating fanout.

**2.4 `DeferredStateEventHandler.Flush*` swap-buffer (§4.9).** Replace `var snapshot = preserveAll.ToArray(); preserveAll.Clear();` (`:103`) and the `latestKeyOrder.ToArray()` mirror at `:116` with two ping-pong `List<>` instances. The flush loop swaps the active buffer pointer atomically per iteration so re-entrant `Notify` calls land in the inactive buffer for the next pass; no per-pass array allocation. Add a regression test that flushes >1 pass and asserts zero allocs after warmup.

**2.5 `Scratchpad` reset hygiene (§4.19).** `Scratchpad.Reset()` (`Store.cs:485`) currently clears only the overlay. Extend to clear `refSet` and `sliceBuffer` so a pooled `MutatorRunner` returned to the pool with stale internals does not surface stale `GetAll<TState>` data on next rental. Add a regression test (one rental that fills `refSet`, returns to pool, second rental asserts empty before any `GetAll` call).

Exit criteria: Phase 0 reentrancy + inline-unsubscribe regressions go green; all existing tests stay green; Phase 0 perf benchmarks for `Notify_50Subs_HalfUnsubscribeInline` show zero alloc after warmup.

### Phase 3 — Indexed slice store + struct enumerator

Goal: `EnumerateAll<TState>` is `O(k)` where k is the count of that state type, not `O(N)` over all slices. Eliminates the boxed iterator and the per-call list shuffling in `FillSlices` (`Store.cs:429–445`). Targets §4.7, §7.3.

**3.1 Secondary index.** Inside `Store`:

- Keep the primary `Map<IReference, Type, Slice>` (canonical) and `Map<IReference, Type, AggregateSlice>` (aggregate) for now. The audit's longer-term §7.3 says replace with `Dictionary<(IReference, Type), Slice>`; that depends on Phase 4 (`IReference` value-equality contract) so we defer the swap to a follow-up.
- Add a `Dictionary<Type, List<BaseSlice>> indexByStateType` updated on every `RegisterSlice` / `UnregisterSlice` / `RegisterAggregate` (and on snapshot apply / prune in `ReregisterCanonicalSliceFromSnapshot` / `PruneCanonicalSlicesNotInSnapshot`).

**3.2 Hot-path consumers.** `FillSlices` collapses to one dictionary lookup + one list iteration. `GetAll<TState>` and `EnumerateAll<TState>` both use the new index.

**3.3 `EnumerateAll<TState>` struct enumerator.** Replace the `IEnumerable<(IReference, TState)>` iterator method with a custom `EnumerateAllResult<TState>` `readonly struct` that exposes a struct enumerator (`MoveNext`, `Current`). The old API stays as an extension that wraps the struct in a `foreach`-friendly shape; consumers iterating with `foreach (var x in store.EnumerateAll<...>())` get the struct path automatically.

**3.4 Aggregate rebuild knock-on.** `AggregateProvider.Build` paths that walk the store benefit indirectly because `GetAll<TState>` now scans only the relevant bucket. No code change in providers — this is a transparent perf win.

Exit criteria: `EnumerateAll_OneTypeBucket_1k` benchmark improves ≥3× ns/op vs Phase 0 baseline; AllocCount drops to 0 (struct enumerator). `Notify_50Subs_HalfUnsubscribeInline` is unchanged. All existing tests green.

### Phase 4 — Contract clarity

Mixed bag of public-API contract hardening. Each item is independently shippable; commit per item. Targets §4.14, §4.15, §4.18, §4.20, §3.7 (analogous to maps Phase 3), and the consumer-friction findings in `## Consumers`.

**4.1 `IReference` becomes `abstract record Reference` (§4.15).** Replace the marker interface in `Runtime/Abstractions/IReference.cs` with:

    public abstract record Reference;
    public sealed record NullReference : Reference { public static NullReference Instance { get; } = new(); }

`Runtime/Utility/Reference.cs` (now under `Abstractions/` after Phase 1 #10) holds the `NullReference` singleton; old `Reference.Null` redirects to `NullReference.Instance` until consumers migrate, then is removed in this same phase. Migrate `EntityStateReference` in `com.scaffold.entities.states/Runtime/EntityStateReference.cs` from `record EntityStateReference : IReference` to `record EntityStateReference : Reference`. Drop the `Scratchpad.ReferenceByValueEqualityComparer` (`Store.cs:554–581`) — records give value equality + hash for free. Update all `IReference?` parameters to `Reference?`.

**4.2 Subscribe callback narrowing (§ Consumers).** Add overloads to `Store` and `IStateEventHandler`:

    void Subscribe<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState;
    void Subscribe<TState>(Reference reference, Action<TState> action) where TState : BaseState;

The old three-arg overload stays; the two new overloads cover the common keyed-subscription case where the consumer already knows its own `Reference`. `TypedSubscription<TState>` gains a `Notify(BaseState, StateChangeEvent)` overload that ignores the redundant reference argument. Audit consumer site `StateEntityIntegrationTests.cs:506–508` (three `(_, _, ev) =>` lambdas) collapses to `(_, ev) => ...` against the new two-arg overload (drops the redundant reference argument; keeps the state argument so the lambda matches the declared `Action<TState, StateChangeEvent>`).

**4.3 Aggregate lifetime API (§4.18, §7.5).** Today `IAggregateProvider.Wire(Store, IAggregateRebuild)` registers subscribers but never returns a teardown handle. Change the contract to:

    interface IAggregateProvider {
        IDisposable Wire(IStoreScope store, IAggregateRebuild rebuild);
        Type AggregateStateType { get; }
        BaseState Build(IStateScope scope);
    }

`AggregateSlice` stores the `IDisposable` and disposes it on a new `Store.UnregisterAggregate(Reference, Type)` (and on `OnAttachedToStore` reattach to prevent the leak path the audit calls out). Add a regression test that registers, unregisters, re-registers an aggregate and asserts the original subscriber list is empty after step 2.

**4.4 `Snapshot` composes, not inherits (§8 / §3.7).** Replace `class Snapshot : Map<IReference, Type, State>` with `sealed class Snapshot { private readonly Map<…> entries; … }` exposing only the methods used by `Store`: `Set`, `Get<TState>`, `TryGet<TState>`, `Contains`, `Count`, and a struct enumerator over the `(reference, type, state)` tuples. The `entries` field stays as a `Map<>` for now; Phase 7 reviews whether it should be a plain `Dictionary<(Reference, Type), State>`.

**4.5 `IStateEventHandler` slim down (§4.14).** Drop the C# 8 default-interface-method (`IStateEventHandler.cs:10–13`); make the two-arg `Notify(reference, state)` an extension method instead. Consumers of the interface stop having to opt out of a default; mocks become trivial.

**4.6 Per-folder docs / public-surface XML (§8).** Add XML doc comments to `Store`, `Mutator<>` / `Mutator<,>`, `IPayloadReference`, `IStateEventHandler`, `IAggregateProvider`, `Reference`. Add a per-folder `README.md` in `Runtime/Pipeline/` and `Runtime/Events/` describing what's internal vs. public and why.

**4.7 Consumer migration in `entities.states`.** Update each Phase-4 contract change at the consumer:

- `EntityStateReference` switches base to `Reference`.
- `StoreInstanceIdExtensions` collapses where the new typed overloads remove the need for an extension (audit calls out 8 wrapper methods; expect 4–6 to evaporate).
- `StateEntityIntegrationTests` lambdas update to the narrower overloads.

Exit criteria: `entities.states` package compiles and all its tests pass against the new contracts; `IReference` removed; all consumer-visible API breakage limited to this phase.

### Phase 5 — Source-generated `MutatorDispatcher`

Goal: eliminate the runtime `Dictionary<Type, List<IPayloadMutatorBinding>>` lookup and the `(TPayload)payload` boxing/cast on every `Execute<TPayload>`. Targets §4.4, §7.1, theme 1, theme 2.

**5.1 Runtime abstraction.** A C# source generator can only contribute code to the assembly being compiled, so a `partial class MutatorDispatcher` emitted into a consumer assembly cannot complete a `partial` declared in the `Scaffold.States` runtime — they are different compilation units. The runtime therefore declares an interface, and the generator emits a concrete `IMutatorDispatcher` implementation in each consumer assembly. New file `Runtime/Pipeline/IMutatorDispatcher.cs`:

    public interface IMutatorDispatcher
    {
        bool TryDispatch<TPayload>(Store store, Reference reference, TPayload payload);
    }

`TryDispatch` returns `false` when no `[Mutator]`-decorated handler exists for `TPayload`, so the `Store` can fall through to the runtime `MutatorRegistry` slow-path. (No `MutatorNotRegisteredException` from inside the dispatcher itself — the throw belongs to `Store.Execute<TPayload>` after both paths miss.)

**5.2 New generator project.** Add `Generators/Scaffold.States.MutatorDispatcherGenerator/` mirroring the existing `Generators/Scaffold.Mvvm.CompositionGenerator/` and `Generators/Scaffold.LiveOps.KeyGenerator/` shape: `*.csproj`, `MutatorDispatcherGenerator.cs` (`[Generator]`), and a `Tests/` project. Output DLL lands in `Analyzers/Output/` alongside the others; `Directory.Build.props` already wires this.

**5.3 Discovery model.** Decorate `Mutator<TState, TPayload>` subclasses with `[Mutator]` (a marker attribute the generator looks for, similar to `[LiveOpsKey]`). The generator scans every assembly that references `Scaffold.States` for `[Mutator]`-decorated `Mutator<TState, TPayload>` subclasses and emits, per consumer assembly, a sealed concrete dispatcher:

    // Generated into the consumer assembly (e.g. Scaffold.EntitiesStates) — not into Scaffold.States.
    public sealed class GeneratedMutatorDispatcher : IMutatorDispatcher
    {
        readonly AddModifierMutator _addModifier = new();
        // … one field per [Mutator]-decorated handler in this assembly …

        public bool TryDispatch<TPayload>(Store store, Reference reference, TPayload payload)
        {
            if (typeof(TPayload) == typeof(AddModifierPayload))
            {
                // Value-type branch: System.Runtime.CompilerServices.Unsafe.As reinterprets the
                // generic local without boxing. Safe under the surrounding typeof equality check.
                ref AddModifierPayload typed = ref Unsafe.As<TPayload, AddModifierPayload>(ref payload);
                _addModifier.Apply(store, reference, typed);
                return true;
            }
            // … one branch per registered (TState, TPayload) pair …
            return false;
        }
    }

The generator emits the value-type branch shown above for `struct` / `readonly record struct` payloads (uses `Unsafe.As<TPayload, T>(ref payload)` from `System.Runtime.CompilerServices`) and a plain `(T)(object)payload` branch for reference-type payloads. The `Unsafe.As` form is the guaranteed zero-alloc path; the `(object)` cast is technically box-eligible IL even when RyuJIT's specialized generic instantiation usually elides it, so we don't rely on that elision for value types. Reference-type payloads cost one cast and zero box. The `typeof(TPayload) == typeof(...)` guard is constant-folded by the JIT for each generic instantiation, so only the matching branch survives per call site. When a consumer's installer wires multiple per-assembly dispatchers, the `Store` can iterate them in registration order (a small `IMutatorDispatcher[]` field); the first non-`false` return wins.

**5.4 Store integration.** `Store` gets a constructor overload that accepts `IMutatorDispatcher? dispatcher` (or `IMutatorDispatcher[] dispatchers` if multi-assembly composition is in scope). `Execute<TPayload>` short-circuits to `dispatcher.TryDispatch(...)` when present; on `false` (or no dispatcher at all) it falls back to the registry for hand-registered mutators. The registry is *not* deleted — it remains the slow-path / runtime registration option. `RegisterMutator<TState, TPayload>` is unchanged; both paths coexist. The throw on "no path matched" lives inside `Store.Execute<TPayload>`, not inside the dispatcher.

**5.5 Consumer migration.** `EntityBridgeContext.RegisterMutators(store)` (`com.scaffold.entities.states/Runtime/EntityBridgeContext.cs:14–19`) — the six hand-rolled `RegisterMutator` calls — collapses to zero lines. Each existing mutator (`AddModifierMutator`, `RemoveModifiersBySourceMutator`, etc.) gets `[Mutator]`, the generator emits `GeneratedMutatorDispatcher` into the `Scaffold.EntitiesStates` assembly, and the consumer's installer (or the new `StatesInstaller` in Phase 6) registers it as the `IMutatorDispatcher` for the `Store`.

**5.6 Regression test for missing-mutator path.** When `Execute<TPayload>` is called with a payload that has no `[Mutator]` and no runtime registration, a typed `MutatorNotRegisteredException` is thrown — same exception as Phase 1 #1. Test in three configurations: dispatcher-present + handler-known, dispatcher-present + handler-absent (falls through to registry), and dispatcher-absent (registry-only).

Exit criteria: `Execute_ValuePayload_OneSlice` benchmark improves ≥3× ns/op vs. its Phase 0 baseline (the `readonly record struct` row added in §0.2); `Execute_SingleMutator_OneSlice` (record-class baseline) improves measurably (no precise multiplier — the baseline already pays only one cast); AllocCount = 0 on the dispatcher path for both shapes. `entities.states` `EntityBridgeContext` no longer hand-registers mutators; the per-assembly `GeneratedMutatorDispatcher` is the only registration site.

### Phase 6 — DI Installer + organization

**6.1 `Container/StatesInstaller`.** New file `Runtime/Container/StatesInstaller.cs`:

    public sealed class StatesInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<StoreBuilder>(Lifetime.Transient);
            builder.RegisterFactory<Store>(c => c.Resolve<StoreBuilder>().Build(), Lifetime.Singleton);
            builder.Register<IStateEventHandler>(_ => StateEventHandlerFactory.CreateDefault(), Lifetime.Singleton);
        }
    }

Add `VContainer` reference to `Scaffold.States.asmdef`. Mirrors theme 8 + audit §7.6.

**6.2 README updates.** Add a `## Public API` table and an end-to-end "Counter store" example wiring `StoreBuilder.RegisterMutator → store.Execute(payload) → store.Subscribe(narrow overload)`. Trim the "recovered from history" line — no longer load-bearing.

**6.3 Samples → `Samples~/`.** Per theme 12, rename `Samples/` to `Samples~/` (Unity convention; tilde-suffixed folders are imported via Package Manager only and do not compile into player builds). Update `package.json` `samples[].path`.

Exit criteria: `git diff` shows the installer file added; `dotnet build -c Release` (or Unity Editor compile) green with VContainer reference in place.

### Phase 7 — Re-benchmark + comparison report

**7.1 Re-run.** Re-run the Phase 0 perf suite (Editor Mono + IL2CPP if available). Save into `Assets/Benchmarks/States/results-phase7.json`.

**7.2 Comparison report.** Author [`Docs/Audits/Packages/Reports/com.scaffold.states.refactor-results.md`](../Docs/Audits/Packages/Reports/com.scaffold.states.refactor-results.md):

- ns/op, bytes/op, AllocCount, Gen0 deltas vs `baselines.json`.
- Highlight: `Execute_SingleMutator_OneSlice` (record-class) zero alloc post-Phase 5; `Execute_ValuePayload_OneSlice` (`readonly record struct`) ≥3× ns/op + zero alloc post-Phase 5 (the §7.1 source-gen win lands hardest on the value-type path because pre-refactor it pays the box on every call); `EnumerateAll_OneTypeBucket_1k` ≥3× speedup post-Phase 3; `Notify_50Subs_HalfUnsubscribeInline` (added in Phase 2 alongside the un-ignored regression test) zero alloc + zero exceptions; `ReferenceEquals_VsReferenceNull` ≥10× faster than `Equals_VirtualDispatch_VsReferenceNull` post-Phase 1.
- Note any byte-counter quirks observed (`Bench.ByteSource` choice).

**7.3 Update `baselines.json` post-refactor.** Replace stale Phase 0 medians with the post-refactor numbers; preserve a copy of the originals in `baselines.phase0.json` for historical diffing.

Gates per `_benchmarking.md`:

- `Allocated`: improve or flat; >10 % regression where the counter is trustworthy fails the PR.
- `Gen0`: must not increase.
- `Time`: >5 % regression flags for review, not auto-fail.

## Concrete Steps

Run from repo root unless noted. Use `cd "path with spaces"` quoting on Windows PowerShell 5.x as per `AGENTS.MD`.

Per-phase command sequence (replace `<phase>` with the phase number):

1. **Compile-only gate before changes:**

       pwsh -NoProfile -File ".agents/scripts/validate-changes.ps1" -SkipTests

2. **Make the phase's edits.** Commit each numbered step in the phase's table separately when they are independently reviewable. Use commit messages of the form `states: phase <phase> — <line item>` (e.g. `states: phase 1 — drop UnityEngine.Debug.LogWarning`).

3. **Re-run the gate:**

       pwsh -NoProfile -File ".agents/scripts/validate-changes.ps1" -SkipTests

4. **EditMode tests:**

       pwsh -NoProfile -File ".agents/scripts/run-unity-tests.ps1" -TestPlatform EditMode

5. **Perf suite (Phase 0 onward):**

       pwsh -NoProfile -File ".agents/scripts/run-unity-tests.ps1" -TestPlatform EditMode -PerformanceTestResultsPath "Assets/Benchmarks/States/results-phase<phase>.json"

6. **Phase 7 only — write `Reports/com.scaffold.states.refactor-results.md`** with the median table + commentary; commit alongside the updated `baselines.json`.

Expected transcript shape on a clean Phase 1 commit:

    Validate-Changes
      Compile               OK
      Asmdef audit          OK
      Pragma gate           OK
      Analyzer check        OK (0 SCA*)

    Test Runner (EditMode)
      Scaffold.States.Tests       33 passed, 0 failed
      Scaffold.States.Tests.Performance  9 passed (ignored: 0)

## Validation and Acceptance

A novice can demonstrate this refactor works by running, after Phase 7, in the project root:

    pwsh -NoProfile -File ".agents/scripts/run-unity-tests.ps1" -TestPlatform EditMode

…and observing:

- Every test under `Assets/Packages/com.scaffold.states/Tests/` passes (the existing seven files plus the new reentrancy and inline-unsubscribe regressions added in Phase 0).
- Every test under `Assets/Packages/com.scaffold.entities.states/Tests/` passes against the migrated contracts.
- The performance suite (`Assets/Benchmarks/States/`) reports zero allocations on the `Execute_SingleMutator_OneSlice` (record class) + `Execute_ValuePayload_OneSlice` (`readonly record struct`) + `EnumerateAll_OneTypeBucket_1k` + `Notify_50Subs_NoMutation` + `Notify_50Subs_HalfUnsubscribeInline` (added in Phase 2) `Bench.NoAllocations` companion tests.

The refactor-results report at `Docs/Audits/Packages/Reports/com.scaffold.states.refactor-results.md` shows the headline deltas:

- Per-call alloc on the typed dispatch path: post-refactor 0 bytes vs. baseline (currently 1× boxed payload + 1× boxed enumerator on `record class`; varies on IL2CPP).
- `EnumerateAll<TState>` over 1000 slices: ≥3× ns/op faster, AllocCount = 0.
- `NotifyReferenceSubscriptions` under inline unsubscribe: zero `InvalidOperationException`, zero per-notify alloc.

Acceptance for each individual phase is the exit criteria stated in that phase's section. A reader wishing to validate one phase in isolation runs the EditMode tests at that commit and confirms the listed regressions/benchmarks behave as specified.

## Idempotence and Recovery

Every phase is additive in the sense that the previous commit is a working revision. If a phase is interrupted:

- Phase 0 can be re-run safely; `baselines.json` overwrites by design.
- Phase 1 items are independent commits; rerunning validate-changes after each is the safety net.
- Phase 2 leaves a working store; the old shared buffers are deleted in the same commit that introduces the rented-buffer pattern, so partial work that compiles is safe to ship.
- Phase 3 introduces the secondary index. If rolled back, `EnumerateAll` falls back to the Phase 2 implementation; tests stay green.
- Phase 4 is the only phase that breaks the consumer. If the consumer migration cannot complete in the same window, hold Phase 4 commits on a feature branch until the consumer is ready; the rest of the plan does not depend on Phase 4 except for §4.1 (`Reference` type) which Phase 5's generator references but can stub against `IReference` if needed.
- Phase 5's generator output is checked into the build via `Analyzers/Output/`; rolling back the generator restores the runtime registry path. Both paths coexist by design.
- Phase 6 is purely additive (installer + docs + folder rename).
- Phase 7 only writes report files; safe to re-run.

If a regression test introduced in Phase 0 is failing for the wrong reason (e.g. flaky in CI), retag with `[Ignore("flaky in <env>; re-enable after <date>")]` and capture the cause in `Surprises & Discoveries`.

## Artifacts and Notes

Indicative diff for Phase 1 #1 (drops `UnityEngine.Debug.LogWarning` and flips `noEngineReferences`):

    Runtime/Store.cs:259-266
    -    private void RunRegisteredMutatorsWithoutCommit(MutatorRunner runner, object payload, IReference executeReference)
    +    private void RunRegisteredMutatorsWithoutCommit(MutatorRunner runner, object payload, Reference executeReference)
         {
             Type payloadType = payload.GetType();
    -        if (!mutatorRegistry.TryGet(payloadType, out IReadOnlyList<IPayloadMutatorBinding>? bindings) || bindings == null || bindings.Count == 0)
    +        if (!mutatorRegistry.TryGet(payloadType, out var bindings))
             {
    -            UnityEngine.Debug.LogWarning($"[Store] No mutators registered for payload type {payloadType.FullName}.");
    -            return;
    +            throw new MutatorNotRegisteredException(payloadType);
             }
             runner.RunMutatorBindingsWithoutCommit(payload, bindings, executeReference);
         }

    Runtime/Scaffold.States.asmdef
    -    "noEngineReferences": false
    +    "noEngineReferences": true

Indicative diff for Phase 4.2 (Subscribe overload narrowing):

    Runtime/Store.cs (Subscriptions region)
    +    public void Subscribe<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState
    +    {
    +        Subscribe<TState>(reference, (_, s, ev) => action(s, ev));
    +    }
    +
    +    public void Subscribe<TState>(Reference reference, Action<TState> action) where TState : BaseState
    +    {
    +        Subscribe<TState>(reference, (_, s, _) => action(s));
    +    }

    com.scaffold.entities.states/Tests/StateEntityIntegrationTests.cs:506-508
    -    store.Subscribe<EntityVariableState>(heroId, (_, _, ev) =>
    -    {
    -        if (ev == StateChangeEvent.Updated) { heroUpdates++; }
    -    });
    +    // Two-arg overload added in §4.2: Action<TState, StateChangeEvent>.
    +    // Drops the redundant reference parameter; keeps state + change-event so the lambda
    +    // matches a declared overload (no Action<StateChangeEvent>-only overload exists).
    +    store.Subscribe<EntityVariableState>(heroId, (_, ev) =>
    +    {
    +        if (ev == StateChangeEvent.Updated) { heroUpdates++; }
    +    });

## Interfaces and Dependencies

End-state public surface that must exist after Phase 6 (paths repository-relative):

- `Assets/Packages/com.scaffold.states/Runtime/Abstractions/Reference.cs`:

       public abstract record Reference;
       public sealed record NullReference : Reference { public static NullReference Instance { get; } = new(); }

- `Assets/Packages/com.scaffold.states/Runtime/Abstractions/IStateEventHandler.cs`:

       public interface IStateEventHandler
       {
           void Notify(Reference reference, BaseState state, StateChangeEvent changeEvent);
           void Subscribe<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState;
           void Unsubscribe<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState;
           void SubscribeAllReferences<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState;
           void SubscribeAny(Action<Reference, BaseState, StateChangeEvent> action);
       }

- `Assets/Packages/com.scaffold.states/Runtime/Store.cs`: keeps the existing `Execute*`, `RegisterSlice`, `RegisterAggregate`, `Save/LoadSnapshot` shape. New: `Subscribe<TState>(Reference, Action<TState, StateChangeEvent>)`, `Subscribe<TState>(Reference, Action<TState>)`, `UnregisterAggregate(Reference, Type)`, optional ctor overload `Store(IStateEventHandler, MutatorRegistry, IMutatorDispatcher?, params BaseSlice[])` (or `IMutatorDispatcher[]` if multi-assembly composition lands).
- `Assets/Packages/com.scaffold.states/Runtime/State/Snapshot.cs`: composes a `Map<>` privately; public surface is `Set`, `Get<TState>`, `TryGet<TState>`, `Contains`, `Count`, struct enumerator.
- `Assets/Packages/com.scaffold.states/Runtime/Pipeline/MutatorRegistry.cs`: unchanged contract; `[NotNullWhen(true)]` annotation on `TryGet`.
- `Assets/Packages/com.scaffold.states/Runtime/Pipeline/IMutatorDispatcher.cs` (new): `bool TryDispatch<TPayload>(Store, Reference, TPayload)`. Implemented by per-consumer generated classes; declared in the runtime so `Store` can depend on it across compilation boundaries.
- `Assets/Packages/com.scaffold.states/Runtime/Pipeline/MutatorAttribute.cs` (new): `[AttributeUsage(AttributeTargets.Class)] public sealed class MutatorAttribute : Attribute { }`.
- `Assets/Packages/com.scaffold.states/Runtime/Pipeline/MutatorNotRegisteredException.cs` (new).
- `Assets/Packages/com.scaffold.states/Runtime/Container/StatesInstaller.cs` (new): VContainer `IInstaller`.
- `Generators/Scaffold.States.MutatorDispatcherGenerator/MutatorDispatcherGenerator.cs` (new): `[Generator]` emitting a sealed `GeneratedMutatorDispatcher : IMutatorDispatcher` into each consumer assembly that contains `[Mutator]`-decorated `Mutator<TState, TPayload>` subclasses.

External consumer (`com.scaffold.entities.states`) end-state changes:

- `Runtime/EntityStateReference.cs`: base type `Reference` (was `IReference`). Same record positional-parameter shape.
- `Runtime/StoreInstanceIdExtensions.cs`: shrinks. Only the wrappers that survive after typed-overload narrowing remain; expect 4–6 of the original 8 methods deleted.
- `Runtime/EntityBridgeContext.cs`: the six `store.RegisterMutator(new …)` lines are gone. Each mutator class is decorated with `[Mutator]`; the generator handles registration.
- All entity mutators (`AddModifierMutator`, `RemoveModifierMutator`, …) gain `[Mutator]`.

## References

- [`Docs/Audits/Packages/com.scaffold.states.md`](../Docs/Audits/Packages/com.scaffold.states.md) — full audit (canonical detail).
- [`Docs/Audits/Packages/com.scaffold.entities.states.md`](../Docs/Audits/Packages/com.scaffold.entities.states.md) — companion consumer audit.
- [`Docs/Audits/Packages/_index.md`](../Docs/Audits/Packages/_index.md) — project-wide themes.
- [`Docs/Audits/Packages/_benchmarking.md`](../Docs/Audits/Packages/_benchmarking.md) — `Bench.Measure` + run policy.
- [`Plans/com.scaffold.maps-refactor-ExecPlan.md`](com.scaffold.maps-refactor-ExecPlan.md) — phased structure model used here.
- [`Assets/Packages/com.scaffold.states/README.md`](../Assets/Packages/com.scaffold.states/README.md) — package-level docs.
- [`AGENTS.MD`](../AGENTS.MD) — repo conventions (DI, generators, asmdef, pure-C# boundaries).
- [`PLANS.md`](../PLANS.md) — ExecPlan authoring requirements.
