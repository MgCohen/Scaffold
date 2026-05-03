# Aggregate rebuild optimization: short-circuit, batch, cache, and auto-track

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

## Purpose / Big Picture

After this work, aggregate rebuilds in `com.scaffold.states` are quieter, cheaper, and easier to author correctly. A consumer building a game on top of the store should observe four user-visible improvements:

- An aggregate that recomputes to the same value as before stops propagating that result. Subscribers (UI, downstream aggregates) are not woken up for no reason.
- A single payload that mutates several canonical slices (for example, drawing a card mutates the deck list and the hand list in one `Execute`) causes each affected aggregate to rebuild **once** at the end of the commit, not once per dependency.
- Mutators that incidentally read an aggregate via `IStateScope.Get<TAggregate>(...)` do not pay an aggregate rebuild on every read inside one commit.
- Aggregate providers can be written without manually listing every dependency in a `Wire(...)` method. The dependency set is discovered automatically from the slices `BuildCore` actually reads, and subscriptions are reconciled on every rebuild.

Someone can see it working by adding the following to a test EditMode run:

- A test that subscribes to a downstream aggregate, then mutates a canonical slice in a way whose recomputed aggregate value is identical to the previous one, observes **zero** notifications on the downstream aggregate. Without this work, the count is `>= 1`.
- A test that issues a single `store.Execute(payload)` whose registered mutators write three different canonical slices, all observed by one player-keyed aggregate, observes the aggregate's rebuild count equal to **1**. Without this work, the count equals **3**.
- A test where a mutator calls `scope.Get<TAggregate>(ref)` three times during one `Execute`, with the underlying canonical state unchanged between reads, asserts that the aggregate's `BuildCore` ran at most **1** time (or 0 if the aggregate was already cached for that scope). Without this work, it ran **3** times.
- A test that registers an aggregate provider whose `BuildCore` calls `scope.Get<X>(refA)` and `scope.Get<Y>(refB)` but does not declare any subscription, and observes that the aggregate still rebuilds when either `X@refA` or `Y@refB` changes, but does not rebuild when an unrelated slice changes. Today this requires a hand-authored `Wire`.

The supporting non-functional outcome: existing tests in `Scaffold.States.Tests` continue to pass. Performance regressions are not introduced for cases that already worked correctly.

## Progress

- [ ] Milestone 0 — Audit findings frozen and acceptance tests authored as red baselines.
- [ ] Milestone 1 — `IStateScope.TryGet<T>(...)` and `Store.TryGet<T>(...)` added; sample `CardGameProviders` migrated off exception-driven absence checks.
- [ ] Milestone 2 — `AggregateSlice` short-circuits notification when the rebuilt state equals the cached state (record value equality).
- [ ] Milestone 3 — `Scratchpad` caches aggregate rebuilds for the duration of one commit.
- [ ] Milestone 4 — Commit-batch coalescing: aggregate rebuilds during a single `Execute` / `ExecuteBatch` collapse to one rebuild per affected aggregate, fired after all canonical Notify calls land.
- [ ] Milestone 5 — Auto-tracked dependencies replace manual `Wire(...)` declarations; existing providers gain a backwards-compatible bridge path.
- [ ] Milestone 6 — Aggregate unregister API plus handler cleanup; ledger entries no longer leak when an aggregate goes away.

## Surprises & Discoveries

Document unexpected behaviors, bugs, optimizations, or insights discovered during implementation. Provide concise evidence.

- (none recorded yet)

## Decision Log

- Decision: Treat **output-level equality short-circuit** as Milestone 2, before any commit-batching work, because it is a five-line change that produces immediately observable benefit and is independently valuable. Rationale: smallest, safest, ships value first.
- Decision: Implement **commit-batch coalescing** as a "dirty set" pass inside `Store.CommitOverlay`, rather than reusing `DeferredStateEventHandler` for the aggregate path. Rationale: `DeferredStateEventHandler` was designed to merge external subscriber notifications, not to gate aggregate provider rebuilds; mixing the two would couple unrelated abstractions. The dirty-set pass lives entirely inside the store's commit phase and does not change subscriber semantics.
- Decision: Keep manual `Wire(...)` working in Milestone 5 as a fallback; do not break existing provider classes. New providers can opt into auto-tracking by leaving `Wire` empty and inheriting from a new base type, while existing providers continue to declare `Wire` subscriptions explicitly. Rationale: keeps the change additive; the existing two `AggregateProvider` samples and the card-game sample's five providers continue to compile without modification.
- Decision: `IStateScope.TryGet<T>(...)` returns `bool` with `out` parameter, matching the .NET convention (`Dictionary.TryGetValue`). Rationale: matches reader expectations, avoids ambiguity around nullable value types, and reuses an idiom every C# author already knows.

## Outcomes & Retrospective

Summarize outcomes, gaps, and lessons learned at major milestones or at completion. Compare the result against the original purpose.

- (to be filled in as milestones complete)

## Context and Orientation

The state package lives at `Assets/Packages/com.scaffold.states/`. The runtime is split across:

- `Runtime/Store.cs` — the central store. Holds two maps: `map: Map<IReference, Type, Slice>` for canonical slices, `aggregates: Map<IReference, Type, AggregateSlice>` for derived aggregates. Owns `MutatorRegistry` and a `Pool<MutatorRunner>`.
- `Runtime/State/AggregateSlice.cs` — the slice variant for derived state. On attach, it calls `provider.Wire` (which subscribes to the provider's dependencies via the store's event handler) and `provider.Build` (which produces the initial value). Subsequent changes go through `RebuildAndNotifyAggregate`, which calls `provider.Build` again and unconditionally fires `Notify(reference, aggregate, StateChangeEvent.Updated)`.
- `Runtime/State/AggregateProvider.cs` — the abstract base providers extend. `Wire(IStoreScope, IAggregateRebuild)` is where authors subscribe to the slices their `BuildCore` reads. `BuildCore(IStateScope)` recomputes the aggregate value from scope.
- `Runtime/Pipeline/MutatorRunner.cs` — temporary scratchpad-backed `IStateScope` used while applying mutators. Reads fall back to the canonical store; writes go to an overlay. `CommitOverlay` calls back into the store to `ApplySnapshot` the overlay, which fires `Set`/`Notify` for each touched canonical slice.
- `Runtime/Events/StateEventHandler.cs` — the default subscription registry. Holds three lists: per-reference subscribers (keyed by `IReference`, then by state type), type-wide subscribers (`SubscribeAllReferences<T>`), and any-state subscribers.
- `Runtime/Events/DeferredStateEventHandler.cs` — an optional event handler that batches and replays subscriber notifications. Not currently in the aggregate path. Will not be used for the rebuild coalescing in Milestone 4 (see Decision Log).

Key terms used throughout this plan:

- **Canonical slice**: a `Slice` instance, registered in `Store.map` at `(IReference, stateType)`. Holds a single `State` record. Mutated by mutators or by direct `RegisterSlice`/`UnregisterSlice` calls. Always present for the duration of registration.
- **Aggregate slice**: an `AggregateSlice` instance, registered in `Store.aggregates`. Holds a derived `AggregateState` record built by an `IAggregateProvider`. Cannot be `Set` directly; rebuilds in response to dependency notifications.
- **Provider Wire**: the optional `Wire(IStoreScope, IAggregateRebuild)` method on `AggregateProvider<T>`. Today it is the only mechanism authors have to declare aggregate dependencies. Subscriptions registered here live for the lifetime of the store. There is no Unsubscribe path.
- **Commit phase**: the body of `Store.CommitOverlay` (which calls `ApplySnapshot`). It iterates the overlay produced by mutators in `Execute`/`ExecuteBatch` and calls `Set` for each entry, which in turn calls `Notify` once per entry. Today, aggregate rebuilds are dispatched synchronously inside `Notify`, so an overlay with N entries can rebuild a downstream aggregate up to N times in one commit.
- **Output short-circuit**: skipping `eventHandler.Notify(...)` when the rebuilt aggregate state equals the previously cached state (per `record` value semantics). Does not skip the rebuild itself, only the propagation.
- **Auto-tracked dependency**: a dependency edge discovered at `BuildCore` time by recording the (reference, state type) of every `IStateScope.Get<T>(...)` call made during build. After build completes, the recorded set replaces the previous subscription set.

Currently relevant tests:

- `Tests/AggregateKeyedCanonicalTests.cs` — exercises a global aggregate over keyed canonical slices; verifies rebuild on register/unregister and snapshot prune.
- `Tests/StoreFeaturesSampleTests.cs` — broad coverage of store features including aggregate notifications.
- `Tests/StoreRegisterAggregateTests.cs` — register/duplicate behavior for aggregates.
- `Tests/CardGameSampleTests.cs` — the card-game sample under `Samples/CardGame/`. Carries one current "documents-existing-behavior" assertion (`playerViewBuilds == 2`) which Milestone 4 will flip to `1`.

## Plan of Work

### Milestone 0 — Audit findings frozen and acceptance tests authored as red baselines

Before changing any production code, add tests that *fail today* and that capture the desired post-change behavior. Place them in `Assets/Packages/com.scaffold.states/Tests/AggregateRebuildOptimizationTests.cs`. Use NUnit `[Test]` attributes following the conventions in `StoreFeaturesSampleTests.cs`. Each test should be independent, build its own `StoreBuilder`, and clean up via NUnit's per-test instance.

Author these tests as **expected-to-fail** and mark them with `[Ignore("Pending Milestone N")]` so the existing CI gate stays green. Each milestone unignores the corresponding tests as it lands.

The tests to author up front:

- `Aggregate_OutputUnchanged_DoesNotNotify` — registers a canonical `CounterState` and an aggregate that returns a constant `AggregateState`; mutates the counter; asserts no aggregate notification fires. Covers Milestone 2.
- `MutatorReadsAggregate_AggregateBuiltAtMostOncePerCommit` — registers an aggregate that increments a counter on every `BuildCore` call; runs a mutator that calls `scope.Get<TAggregate>` three times; asserts the build counter increased by at most one. Covers Milestone 3.
- `Execute_MultiSlicePayload_AggregateRebuildsOnce` — registers two canonical slices and one player-keyed aggregate that depends on both; runs a payload bound to two mutators (one per slice); subscribes to the aggregate; asserts the subscriber received exactly one notification. Covers Milestone 4. Once Milestone 4 lands, the existing `DrawDispatchesAllZoneMutatorsInOneCommit` test in the card-game sample should be updated to assert `playerViewBuilds == 1`.
- `AutoTracked_RebuildsForReadDependencies_NotForUnreadOnes` — registers an aggregate provider with an empty `Wire` and a `BuildCore` that reads `scope.Get<X>(refA)` and `scope.Get<Y>(refB)`. Mutates `X@refA` and asserts the aggregate rebuilt; mutates `Z@refC` (unread) and asserts the aggregate did not rebuild. Covers Milestone 5.

Each subsequent milestone is considered done only when the corresponding test transitions from ignored-and-failing to running-and-passing.

### Milestone 1 — IStateScope.TryGet for absent slices

Add a non-throwing read to the scope so providers can model "this slice may be absent" without using exceptions for control flow. The card-game sample (`Samples/CardGame/CardGameProviders.cs`) already needs this for the visibility / face-down pattern; today it wraps `scope.Get<T>` in `try/catch (KeyNotFoundException)`, which allocates and unwinds on every face-down rebuild.

Add to `Assets/Packages/com.scaffold.states/Runtime/Abstractions/IStateScope.cs`:

    bool TryGet<TState>(IReference? reference, out TState state) where TState : BaseState;

Return `true` and assign `state` when the slice exists; return `false` and leave `state` at `default!` otherwise. The reference parameter accepts null, normalizing to `Reference.Null` consistent with `Get<T>`.

Implement in two places:

- `Store.TryGetSlice` is already present internally in `Store.cs` (around line 453). Expose a public `TryGet<TState>` that delegates to it. Aggregate slices return their cached `State` (do not invoke `BuildForScope` from `TryGet` — keep it cheap).
- `Store.Scratchpad.TryGet` mirrors `Store.Scratchpad.Get` but never throws. It checks the overlay first, then asks the owner store. For aggregate slices encountered through the overlay path it should still call `BuildForScope` because that's the contract `Get` already follows; the difference is only that the missing case returns `false` instead of throwing.

Update the sample at `Samples/CardGame/CardGameProviders.cs` to use the new method:

    private static T? TryGetState<T>(IStateScope scope, IReference reference) where T : BaseState
    {
        return scope.TryGet<T>(reference, out var s) ? s : null;
    }

Remove the `try/catch (KeyNotFoundException)` wrapper. The helper above is local-to-file; consider whether to promote it to a static extension class `Scaffold.States.StateScopeExtensions` if more callers appear, but do not pre-emptively add the extension.

Add a test `Store_TryGet_ReturnsFalseForAbsentSlice` and `Store_TryGet_ReturnsCachedAggregateState` to `Tests/StoreFeaturesSampleTests.cs`.

### Milestone 2 — Output equality short-circuit

In `Runtime/State/AggregateSlice.cs`, change `RebuildAndNotifyAggregate` so that when the rebuilt value equals the cached value, the slice does not replace `State` and does not fire `Notify`:

    private void RebuildAndNotifyAggregate()
    {
        if (attachedStore is null)
        {
            throw new InvalidOperationException("Aggregate slice is not attached to a store.");
        }

        var built = (AggregateState)provider.Build(attachedStore);
        if (Equals(State, built))
        {
            return;
        }

        State = built;
        attachedStore.Events.Notify(Reference, built, StateChangeEvent.Updated);
    }

This relies on `record` value equality. Most aggregate states declared in samples and tests are positional records of value types and strings, where this works correctly out of the box.

Document the gotcha in the existing `AggregateProvider` XML comments: aggregate state records that hold collection fields (`IReadOnlyList<T>`, arrays) compare those fields by reference. To make the short-circuit effective for collection-bearing aggregates, authors should either (a) override `Equals` on the record to use `SequenceEqual` for collection fields, or (b) preserve the same collection instance when the contents have not changed. Add these notes to the AggregateProvider summary, not to a separate doc, so they show up at intellisense time.

Acceptance: `Aggregate_OutputUnchanged_DoesNotNotify` passes. The existing `Aggregate_Subscribe_ReceivesRebuiltStateAfterCanonicalCommit` test in `StoreFeaturesSampleTests.cs` continues to pass because the relevant payload always changes the aggregate value.

### Milestone 3 — Scratchpad aggregate rebuild cache

When a mutator calls `scope.Get<TAggregate>(ref)`, the current `Scratchpad.Get` path (in `Store.cs` around line 542) detects the slice is an `AggregateSlice` and calls `aSlice.BuildForScope(this)` for every read. Three reads of the same aggregate during one mutator commit therefore call `BuildCore` three times.

Add a per-commit cache on `Scratchpad` keyed by `(IReference, Type)`:

- A `Dictionary<(IReference, Type), AggregateState>` field initialized in the constructor.
- `Reset()` (already called when the runner is returned to the pool) clears it.
- In `Get<TState>(IReference?)`, when a slice is an `AggregateSlice` and the cache contains an entry for `(reference, typeof(TState))`, return the cached value; otherwise call `BuildForScope`, store the result, return it.
- Invalidate the cache entry for an aggregate when any of its dependencies appears in the overlay. The simplest invariant: clear the entire aggregate cache whenever `SetPending` writes a canonical slice. This is correct (no stale reads possible) and cheap (a `Clear()` on a small dict). A more sophisticated dep-tracking invalidation could come with Milestone 5, but is not required for correctness here.

Acceptance: `MutatorReadsAggregate_AggregateBuiltAtMostOncePerCommit` passes. Existing tests pass.

### Milestone 4 — Commit-batch coalescing

The largest behavioral change. Today, `Store.CommitOverlay` calls `ApplySnapshot`, which iterates overlay entries and calls `Set` for each. `Set` calls `eventHandler.Notify`, which synchronously runs subscriber callbacks, which call `IAggregateRebuild.RequestRebuild()`, which synchronously rebuilds and notifies further. The result: an overlay touching N slices that affect one downstream aggregate causes N rebuilds of that aggregate.

Replace this with mark-dirty during the commit, rebuild-once after the commit:

- Add to `AggregateSlice` an internal `bool isDirty` flag (or store dirty references on the store; either is acceptable, prefer the slice for locality).
- Change `RebuildCallback.RequestRebuild()` so that when the store is currently inside an active commit phase, it sets `isDirty = true` and returns; otherwise it falls through to `RebuildAndNotifyAggregate()` synchronously (preserving the existing post-commit-time behavior for `RegisterSlice`/`UnregisterSlice`/manual mutator dispatch outside the runner).
- Add to `Store` a flag `inCommit` that `ApplySnapshot` sets to `true` for the duration of its loop.
- After `ApplySnapshot` finishes its loop and clears `inCommit`, walk the dirty aggregate set in registration order and call `RebuildAndNotifyAggregate` on each. Combined with Milestone 2's short-circuit, an aggregate whose final post-commit value equals its previous cached value will not even fire the post-commit notification.

The mark-dirty path must be re-entrant safe: a downstream aggregate's rebuild that mutates *no* canonical state but causes another aggregate's `RequestRebuild` (because aggregate-emitted notifications are not gated by `inCommit`) must continue to work. The simplest way: aggregate-emitted notifications fire through the same `Notify` path, so subscribers' `RequestRebuild` calls hit the same dirty-set code, which detects the slice is already dirty and is a no-op for that pass.

Update `LoadSnapshot` similarly: it currently calls `Set` and `eventHandler.Notify` per restored entry, which would otherwise miss the coalescing. Wrap the restoration loop in the same `inCommit` flag.

Acceptance: `Execute_MultiSlicePayload_AggregateRebuildsOnce` passes. Once green, change the assertion in `Tests/CardGameSampleTests.cs::DrawDispatchesAllZoneMutatorsInOneCommit` from `Is.EqualTo(2)` to `Is.EqualTo(1)` and remove the foreshadowing comment. Existing tests must still pass; in particular, `Aggregate_Subscribe_ReceivesRebuiltStateAfterCanonicalCommit` should observe exactly one notification per `Execute`, not multiple.

### Milestone 5 — Auto-tracked dependencies

The most invasive change, and the one that pays back the most in author ergonomics. After this milestone, `Wire` is optional; new providers can omit it entirely and the dependency set is discovered from `BuildCore` by recording every `IStateScope.Get<T>(...)` call.

Implementation:

- Introduce a tracking scope wrapper. Rename or add a new file `Runtime/State/TrackingStateScope.cs` (internal). It wraps an `IStateScope`, forwards `Get<T>` and `GetAll<T>` calls to the wrapped scope, and records the tuple `(typeof(T), IReference)` for each `Get`. `GetAll<T>` records `(typeof(T), null)` to mean "all references of this type."
- In `AggregateSlice.OnAttachedToStore`, build the initial value through a `TrackingStateScope` rather than directly through the store. After build, install subscriptions for the recorded set. Recorded `(T, ref)` becomes `Subscribe<T>(ref, ...)`. Recorded `(T, null)` becomes `SubscribeAllReferences<T>(...)`.
- In `RebuildAndNotifyAggregate`, again build through a `TrackingStateScope`, then **diff** the recorded set against the previous set: subscribe to new entries, unsubscribe from gone-away entries. Keep one stable handler per `(T, ref)` slot so that unsubscribe by delegate identity works.
- Add an opt-in / opt-out switch on the provider. The cleanest approach is a new abstract base `AutoTrackedAggregateProvider<TAggregate>` that overrides `Wire` to do nothing (and seals it) and signals to the `AggregateSlice` that it should auto-track. Existing `AggregateProvider<TAggregate>` keeps its current contract: explicit `Wire`, no auto-tracking. `IAggregateProvider` gains a `bool AutoTrack { get; }` property with a default implementation returning `false` (default-interface-method, requires C# 8+; if the project's language version forbids that, expose it as a virtual property on `AggregateProvider` and override it on the auto-tracked base).

Bridge for current providers: the `CardGameProviders.cs` `HandViewProvider` and `DiscardViewProvider` both currently use `SubscribeAllReferences<CardView>`. Once auto-tracking exists, those providers can be migrated to derive from `AutoTrackedAggregateProvider<HandView>` / `AutoTrackedAggregateProvider<DiscardView>` and delete the `Wire` overrides — the per-card subscriptions will be installed automatically based on which `CardId`s `BuildCore` actually reads. This both demonstrates the new model and is the specific fix to the "broad subscribe" review feedback on PR #37 (CodeRabbit nitpick at `CardGameProviders.cs:63-68`).

Edge cases to handle:

- **`scope.GetAll<T>()`**: enumerates all references of a state type. The auto-tracker records this as a "wildcard on T" subscription. Whenever any `T@*` changes, the aggregate rebuilds. This matches today's `SubscribeAllReferences` behavior. If the aggregate previously read an explicit `T@refA` and now reads `GetAll<T>()`, the wildcard subscription replaces and obsoletes the explicit one.
- **Conditional reads**: a `BuildCore` that reads X then conditionally reads Y based on X's value will subscribe to Y only after a build that took the Y branch. That is correct: rebuilds re-track, so once Y is read it joins the dep set. The first build after a state shape that does not read Y, Y is correctly removed from the set and no longer triggers rebuild.
- **Aggregate-of-aggregate**: reading another aggregate via `scope.Get<OtherAggregate>(ref)` records `(OtherAggregate, ref)` and subscribes to its notifications. This is the same pattern as today's manual `Subscribe<OtherAggregate>(ref, rebuild)` and is well-defined.

Acceptance: `AutoTracked_RebuildsForReadDependencies_NotForUnreadOnes` passes. Existing providers continue to compile and pass tests with no changes. Migrate `HandViewProvider` and `DiscardViewProvider` to the auto-tracked base in this milestone and delete their `Wire` bodies; the existing card-game tests must remain green.

### Milestone 6 — Aggregate unregister and handler cleanup

Today there is no API to unregister an aggregate post-build. Subscriptions installed by `Wire` (or, after Milestone 5, by the tracker) live forever in `StateEventHandler.Subscriptions`. This is acceptable for the lifetime of a session, but becomes a leak in long-lived sessions where aggregates come and go (for example, transient per-screen aggregates). With Milestone 5, the auto-tracker already needs an unsubscribe-by-delegate path to handle dep diffs; expose the unregister API on top of that.

Add to `Store`:

- `bool UnregisterAggregate<TAggregate>(IReference? reference) where TAggregate : AggregateState`
- A non-generic overload `UnregisterAggregate(IReference? reference, Type aggregateStateType)` consistent with `UnregisterSlice`.
- The unregister path tears down the slice's installed subscriptions (auto-tracked or manually `Wire`'d), removes the slice from `Store.aggregates`, and fires `Notify(reference, lastState, StateChangeEvent.Removed)`.

For manually `Wire`'d providers (the existing `AggregateProvider<T>` path), we need a way to unsubscribe the handlers `Wire` installed. Two options:

- Have `Wire` return an `IDisposable`-like aggregate-subscription scope that the slice retains; on unregister, dispose it. This is the cleanest model long term.
- Alternatively, track every `IStateEventHandler.Subscribe` / `SubscribeAllReferences` call made through a wrapper passed to `Wire`. The wrapper records the delegates so the slice can unsubscribe them en masse. Less invasive on the API surface.

The second approach is preferred for backwards compatibility: the `Wire` method signature does not change, but the `IStoreScope` it receives is a tracking wrapper. Existing providers compile unchanged; the slice tears down their subscriptions automatically on unregister.

Acceptance test: `Aggregate_Unregister_RemovesAllSubscriptions` registers an aggregate that increments a counter on every rebuild, unregisters it, then mutates a canonical slice that *was* a dependency. The counter must not increase after unregister.

## Validation

Run the full state package test suite after each milestone:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests

Open the Unity Test Runner and execute the `Scaffold.States.Tests` assembly. All tests under that assembly must pass at every milestone boundary. The card-game sample tests in particular exercise multi-slice payloads and visibility flips and are a useful integration smoke.

For Milestones 2, 3, 4, and 5, check the relevant acceptance test transitions from `[Ignore]` to a passing test. Do not delete the `[Ignore]` attribute prematurely — only when the corresponding production code lands.

## Risks and rollback

The work is sequenced specifically so that each milestone is independently shippable and independently revertable. If Milestone 4's commit-batch coalescing exposes a behavioral regression in a downstream consumer that depended on per-slice notification ordering, it can be rolled back without losing Milestone 1, 2, or 3. Milestone 5 is the only milestone that introduces a new abstract base class; if it must be reverted, providers that migrated to `AutoTrackedAggregateProvider` need to be moved back to manual `Wire`. Avoid migrating production providers in the same change that introduces the base; do those as separate commits so the migration commit can be reverted in isolation.
