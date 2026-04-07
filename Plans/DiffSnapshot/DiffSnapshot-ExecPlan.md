# Add diff snapshot strategy to the States package

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.


## Purpose / Big Picture

Today `Store` owns all snapshot logic directly: `SaveSnapshot` walks its internal map and produces a `Snapshot`, and `LoadSnapshot` applies entries then prunes. This makes adding new snapshot strategies (diff, log-replay) require adding new methods to `Store` for each one.

After this work, snapshot behavior is fully extracted behind an `ISnapshotHandler` interface. `Store` holds an injected handler and its `SaveSnapshot` / `LoadSnapshot` methods simply delegate, passing a controlled scope object that grants the handler read and write access to the canonical slice collection. The handler decides what data to collect on save and how to apply it on load, using an internal strategy dispatch on the snapshot type. Adding a new snapshot strategy in the future means implementing a new handler — nothing in `Store` changes.

The first two concrete strategies delivered here are `FullSnapshotHandler` (the existing full save/load behavior, now extracted) and `DiffSnapshotHandler` (new — saves only slices that changed since the last baseline, loads only those, leaves everything else intact). `FullSnapshotHandler` is the default; callers who want diff behavior configure `DiffSnapshotHandler` via `StoreBuilder`.

Someone can see the feature working when: (1) all existing snapshot behavior passes through `FullSnapshotHandler` with no visible change to callers, (2) tests in `Assets/Packages/com.scaffold.states/Tests/` prove that `DiffSnapshotHandler.Save()` produces only changed entries and `Load()` applies them without touching untouched slices, and (3) `validate-changes.ps1 -SkipTests` exits clean.

A third handler — log/mutation-replay — is explicitly out of scope here. The `ISnapshotHandler` + `IStoreSnapshotScope` pattern is its forward-compatible extension point.


## Progress

- [x] Author initial ExecPlan at `Plans/DiffSnapshot/DiffSnapshot-ExecPlan.md` (this file).
- [ ] Milestone 1 — Shared contracts: `ISnapshot` marker interface, `SliceEntry` record, `IStoreSnapshotScope` interface, `ISnapshotHandler` interface. No behavior changes yet.
- [ ] Milestone 2 — Data types: `Snapshot` gains `: ISnapshot`. `DiffSnapshot` is introduced (uses `StateChangeEvent` as ledger value, implements `ISnapshot`).
- [ ] Milestone 3 — Handlers: `FullSnapshotHandler` (snapshot logic extracted from `Store`) and `DiffSnapshotHandler` (event-driven, new strategy).
- [ ] Milestone 4 — Store and StoreBuilder wiring: `Store` injects `ISnapshotHandler`, gains private `SnapshotScope`, delegates `SaveSnapshot`/`LoadSnapshot`. `StoreBuilder` gains `AddSnapshotHandler`, defaults to `FullSnapshotHandler`.
- [ ] Milestone 5 — Tests: cover both handlers and the Reset contract.
- [ ] Milestone 6 — Documentation, quality gate, commit.


## Surprises & Discoveries

- (none yet)


## Decision Log

- Decision: Extract all snapshot logic from `Store` into an `ISnapshotHandler`. `Store.SaveSnapshot()` and `Store.LoadSnapshot()` become one-line delegations.
  Rationale: The user explicitly asked for a policy/implementation approach: one consistent abstraction with swappable strategies. Each future strategy is a new class, not a new method on `Store`. `Store` stays focused on slice registration, mutator execution, and event dispatch.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: Handlers receive a per-call `IStoreSnapshotScope` rather than a Store reference or making Store methods public.
  Rationale: The user asked for "Store passes an interfaced scope that allows Add/Remove/Change slices." The scope is a private nested class inside `Store`, giving it full access to internal state without exposing anything publicly. Passing it per-call (not at construction) keeps handlers stateless with respect to their Store dependency; the diff handler's statefulness comes only from its event subscription and ledger.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: `IStoreSnapshotScope` exposes three operations: `GetCanonicalSlices()` (returns `IReadOnlyList<SliceEntry>`), `Apply(IReference, State)` (upsert — registers a new slice if absent, sets state if present), and `Remove(IReference, Type)` (unregisters).
  Rationale: These are the three primitive operations any snapshot handler needs. `Apply` does the upsert internally (checks the canonical map, calls `RegisterSlice` or the private `Set`), so handlers do not need to distinguish between new and existing slices. `GetCanonicalSlices` is needed by `FullSnapshotHandler` to enumerate all rows for a full save and for the prune step in a full load. `DiffSnapshotHandler` does not call `GetCanonicalSlices` (it tracks state via events), but the interface provides it for completeness.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: `SliceEntry` is a public `sealed record` with properties `IReference Reference`, `Type StateType`, and `State State`. No C# tuples anywhere in the public surface.
  Rationale: Tuples carry no semantic names and are fragile as record/key types. `SliceEntry` gives the payload a meaningful name and is consistent with the no-tuples rule requested in prior sessions.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: Drop the earlier `SliceChangeKind` enum. Use the existing `StateChangeEvent` (`Created`, `Updated`, `Removed`) as the value type in `DiffSnapshot.ChangeMap` and in `DiffSnapshotHandler`'s internal ledger.
  Rationale: `StateChangeEvent` already expresses all three outcomes. Adding a parallel enum with equivalent values is redundant. `Created` and `Updated` are both treated as "apply state" on load; `Removed` is treated as "remove slice."
  Author: ExecPlan revision 2, 2026-04-07 (carried forward).

- Decision: `ISnapshotHandler` is non-generic. `Store.SaveSnapshot()` returns `ISnapshot` and `Store.LoadSnapshot(ISnapshot)` accepts any `ISnapshot`. The handler casts internally using a type switch.
  Rationale: `Store` cannot be generic over the snapshot type without major API churn. A non-generic handler with a marker interface is the standard approach. Callers who know their handler type can access it via `StoreBuilder`-retained references to get strongly-typed return values.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: `ISnapshotHandler` includes a `Reset()` method. `FullSnapshotHandler.Reset()` is a no-op. `DiffSnapshotHandler.Reset()` clears its internal ledger and pending state cache.
  Rationale: Any stateful handler needs a baseline-reset operation. Placing it on the interface ensures callers always have the reset contract available regardless of which handler is configured.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: `DiffSnapshotHandler` receives `IStateEventHandler` at construction and subscribes via `SubscribeAny`. It does not hold a `Store` reference.
  Rationale: `IStateEventHandler.SubscribeAny` is public and is exactly the subscription point needed. Receiving the event handler avoids a circular dependency (Store holds handler, handler holds Store). The builder wires them explicitly: the same `IStateEventHandler` instance is passed to both `StoreBuilder.AddEventHandler` and `DiffSnapshotHandler`'s constructor.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: `DiffSnapshotHandler.Load` calls `Reset()` at the end of its own load operation.
  Rationale: `scope.Apply` and `scope.Remove` both fire `Notify` events, which the handler observes via its event subscription. Without a reset at the end of `Load`, those load-side writes would populate the ledger and corrupt the next `Save()`. Auto-resetting inside `Load` makes the postcondition clear: after `Load`, the baseline is the post-load store state.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: Callers must call `handler.Reset()` explicitly after `store.SaveSnapshot()` or `store.LoadSnapshot()`. `Store` cannot call it because it knows nothing about which handler is in use beyond the interface.
  Rationale: `Store.LoadSnapshot(snapshot)` delegates to the handler's `Load`, so `FullSnapshotHandler.Load` has no reset to call. If the user has a `DiffSnapshotHandler`, they must reset it after a full-snapshot load because `FullSnapshotHandler.Load` fires `Apply`/`Remove` events that the diff handler observes. Documenting this contract in the README makes it explicit.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: `Store.SaveSnapshot()` return type changes from `Snapshot` to `ISnapshot`. `Store.LoadSnapshot` parameter changes from `Snapshot` to `ISnapshot`. This is a breaking API change on `Store`.
  Rationale: The unified delegation pattern requires a shared base type. Callers that used `Snapshot` directly can cast: `(Snapshot)store.SaveSnapshot()` when `FullSnapshotHandler` is configured. The existing `Snapshot` type is unchanged; only the Store method signature evolves. Existing tests that go through `StoreBuilder` are unaffected except for the cast; tests that constructed `Store` directly need to pass a snapshot handler.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: `Store`'s private `ApplySnapshot` method is retained unchanged for `Scratchpad.Commit()` (the mutator overlay commit path). It is not exposed on `IStoreSnapshotScope` and is not related to the snapshot handler.
  Rationale: `Scratchpad` uses `ApplySnapshot` to merge the mutator overlay into committed slices. This is an internal concern of the mutator pipeline, not a snapshot concern. The two paths are separate.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: `PruneCanonicalSlicesNotInSnapshot` is removed from `Store`. Its logic moves into `FullSnapshotHandler.Load`, which uses `scope.GetCanonicalSlices()` and `scope.Remove()`.
  Rationale: This private method was only ever called by `LoadSnapshot`. With snapshot logic moved to the handler, it belongs in the handler.
  Author: ExecPlan revision 3, 2026-04-07.

- Decision: Aggregate slices are excluded from all snapshots. `IStoreSnapshotScope.GetCanonicalSlices()` returns only canonical slices (iterates `Store.map`, not `Store.aggregates`). `DiffSnapshotHandler.OnStateChanged` ignores callbacks where `state is not State` (i.e. `AggregateState` callbacks are skipped).
  Rationale: Aggregates are derived views rebuilt from canonical slices. Persisting them would create stale derived values.
  Author: ExecPlan revision 3, 2026-04-07 (carried forward from prior revisions).

- Decision: The future log-based snapshot handler will implement `ISnapshotHandler` and subscribe to store events (and potentially `Execute` interception via a mutator wrapper). No changes to this ExecPlan needed.
  Rationale: The interface + scope pattern established here is the forward-compatible slot.
  Author: ExecPlan revision 3, 2026-04-07.


## Outcomes & Retrospective

(Not yet written — fill in after Milestone 6.)


## Context and Orientation

### Repository layout for this ExecPlan

All work is in `Assets/Packages/com.scaffold.states/`. The runtime source is in `Runtime/` and tests in `Tests/`. Within `Runtime/`, relevant subdirectories are:

- `Abstractions/` — public interfaces (`IStateEventHandler`, `IStateScope`, `IStoreScope`, and after this work: `ISnapshot`, `IStoreSnapshotScope`, `ISnapshotHandler`)
- `State/` — canonical state types (`Snapshot`, `BaseState`, `State`, `Slice`, etc., and after this work: `SliceEntry`, `DiffSnapshot`, `FullSnapshotHandler`, `DiffSnapshotHandler`)
- `Pipeline/` — internal mutator pipeline (`IStoreScratchpad`, `MutatorRunner`, etc. — not touched by this work)
- `Builders/Store/` — `StoreBuilder` and builder methods

### Key existing files (read them before editing)

`Assets/Packages/com.scaffold.states/Runtime/Store.cs` — central class. Constructor signature: `Store(IStateEventHandler eventHandler, MutatorRegistry mutatorRegistry, params BaseSlice[] slices)`. Private fields: `map` (`Map<IReference, Type, Slice>`), `aggregates` (`Map<IReference, Type, AggregateSlice>`), `eventHandler`, `mutatorRegistry`. Snapshot region (lines ~64–121): `SaveSnapshot()` returns `Snapshot`, `LoadSnapshot(Snapshot)`, private `ApplySnapshot(Snapshot)` (keep — used by `Scratchpad`), private `PruneCanonicalSlicesNotInSnapshot` (remove — moves to handler). Private `Set(IReference, State)` handles upsert into canonical slices; called by `ApplySnapshot` and directly by the `SnapshotScope.Apply` implementation.

`Assets/Packages/com.scaffold.states/Runtime/State/Snapshot.cs` — `Map<IReference, Type, State>` subclass with typed `Get`/`Set` helpers. After this work it also implements `ISnapshot`.

`Assets/Packages/com.scaffold.states/Runtime/State/StateChangeEvent.cs` — existing public enum: `Created`, `Updated`, `Removed`. Used as the value type in `DiffSnapshot.ChangeMap`.

`Assets/Packages/com.scaffold.states/Runtime/Abstractions/IStateEventHandler.cs` — public interface. Relevant here: `void SubscribeAny(Action<IReference, BaseState, StateChangeEvent> action)`. `DiffSnapshotHandler` calls this at construction.

`Assets/Packages/com.scaffold.states/Runtime/Builders/Store/StoreBuilder.cs` — currently builds `Store` with `new Store(stateHandler, mutatorRegistry, entries.ToArray())`. After this work the constructor call adds a snapshot handler argument and the builder gains `AddSnapshotHandler(ISnapshotHandler)`.

`Assets/Packages/com.scaffold.maps/Runtime/Map.cs` — `Map<TPrimary, TSecondary, TValue>`. The `[primary, secondary]` indexer upserts. `Clear()` resets. `Remove(primary, secondary)` removes one entry. Inherited by `Snapshot`. `Count` comes from `BaseMap`; verify it is accessible before using `snapshot.Count == 0` in `IsEmpty`.

### Term definitions

**ISnapshot** — a public marker interface. Implemented by `Snapshot` (full) and `DiffSnapshot` (diff). `Store.SaveSnapshot()` returns `ISnapshot`. Handlers cast to the concrete type internally.

**IStoreSnapshotScope** — a public interface that `Store` instantiates as a private nested class and passes to the handler on every `SaveSnapshot`/`LoadSnapshot` call. It exposes: `GetCanonicalSlices()`, `Apply(reference, state)` (upsert), and `Remove(reference, type)`.

**SliceEntry** — a public `sealed record` carrying the three dimensions of a canonical slice: `IReference Reference`, `Type StateType`, `State State`.

**ISnapshotHandler** — a public interface with three methods: `ISnapshot Save(IStoreSnapshotScope scope)`, `void Load(IStoreSnapshotScope scope, ISnapshot snapshot)`, `void Reset()`.

**FullSnapshotHandler** — stateless implementation of `ISnapshotHandler`. On `Save`, iterates all canonical slices via scope and builds a `Snapshot`. On `Load`, applies all entries via scope then prunes missing canonical slices via scope. `Reset()` is a no-op.

**DiffSnapshotHandler** — stateful implementation of `ISnapshotHandler`. Subscribes to `IStateEventHandler.SubscribeAny` at construction. Tracks two internal maps: a `Map<IReference, Type, StateChangeEvent>` ledger (what changed) and a `Snapshot` of pending state values (latest value for each Created/Updated key). On `Save`, drains both into a `DiffSnapshot`. On `Load`, applies changes via scope, removes tombstones via scope, then calls `Reset()`. On `Reset()`, clears both internal maps.

**Baseline** — the point in time from which the diff handler measures changes. After `Save()` or `Reset()`, the ledger is empty and the current store state is implicitly the new baseline.


## Plan of Work

### Milestone 1 — Shared contracts

Create four new files. No existing files are modified yet.

**File 1: `Assets/Packages/com.scaffold.states/Runtime/Abstractions/ISnapshot.cs`**

A public marker interface with no members. `Snapshot` and `DiffSnapshot` will implement it.

    namespace Scaffold.States
    {
        /// <summary>Marker interface for snapshot data produced and consumed by <see cref="ISnapshotHandler"/>.</summary>
        public interface ISnapshot
        {
        }
    }

**File 2: `Assets/Packages/com.scaffold.states/Runtime/State/SliceEntry.cs`**

A public sealed record. One instance per canonical slice row.

    namespace Scaffold.States
    {
        /// <summary>Carries the three dimensions of a canonical slice row for use by snapshot handlers.</summary>
        public sealed record SliceEntry(IReference Reference, Type StateType, State State);
    }

Add `using System;` at the top since `Type` lives in `System`.

**File 3: `Assets/Packages/com.scaffold.states/Runtime/Abstractions/IStoreSnapshotScope.cs`**

A public interface. The private `SnapshotScope` nested class inside `Store` will implement it.

    using System;
    using System.Collections.Generic;

    namespace Scaffold.States
    {
        /// <summary>
        /// Controlled access to canonical slice state passed by <see cref="Store"/> to <see cref="ISnapshotHandler"/> on each save or load.
        /// Implementations are private to <see cref="Store"/>; do not implement this interface outside the states assembly.
        /// </summary>
        public interface IStoreSnapshotScope
        {
            /// <summary>Returns a snapshot of all current canonical slice rows. Safe to enumerate while calling Apply or Remove on other keys.</summary>
            IReadOnlyList<SliceEntry> GetCanonicalSlices();

            /// <summary>
            /// Registers a new canonical slice if none exists for this key, or replaces the committed state if one does.
            /// Fires <see cref="StateChangeEvent.Created"/> or <see cref="StateChangeEvent.Updated"/> accordingly.
            /// </summary>
            void Apply(IReference reference, State state);

            /// <summary>
            /// Removes the canonical slice for this key if present. Returns without error if absent.
            /// Fires <see cref="StateChangeEvent.Removed"/> when a slice is found and removed.
            /// </summary>
            void Remove(IReference reference, Type stateType);
        }
    }

**File 4: `Assets/Packages/com.scaffold.states/Runtime/Abstractions/ISnapshotHandler.cs`**

A public interface. Stateless handlers like `FullSnapshotHandler` will implement `Reset()` as a no-op. Stateful handlers like `DiffSnapshotHandler` use it to clear their ledger.

    namespace Scaffold.States
    {
        /// <summary>
        /// Strategy for producing and consuming snapshot data. Injected into <see cref="Store"/> via <see cref="StoreBuilder"/>.
        /// The handler receives a per-call <see cref="IStoreSnapshotScope"/> for slice access rather than a direct reference to the store.
        /// </summary>
        public interface ISnapshotHandler
        {
            /// <summary>Produces a snapshot of the store's current state (or a delta since the last baseline, depending on the implementation).</summary>
            ISnapshot Save(IStoreSnapshotScope scope);

            /// <summary>Applies a snapshot to the store. Implementations dispatch on the concrete <see cref="ISnapshot"/> type.</summary>
            void Load(IStoreSnapshotScope scope, ISnapshot snapshot);

            /// <summary>
            /// Clears any internal tracking state and re-establishes the current store state as the baseline.
            /// Callers must invoke this after <see cref="Store.SaveSnapshot"/> or <see cref="Store.LoadSnapshot"/> when a stateful handler
            /// (such as <see cref="DiffSnapshotHandler"/>) is in use alongside a full-snapshot operation.
            /// </summary>
            void Reset();
        }
    }

After creating all four files, run the quality gate to confirm the assembly compiles with no new errors:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests

**Unity meta files:** Use Unity MCP if the editor bridge is active. If unavailable, copy an adjacent `.meta` from the same subfolder, replace `guid` with a fresh 32-character hex string. Check existing sibling `.meta` files for the YAML shape.


### Milestone 2 — Data types

Modify `Snapshot.cs` and create `DiffSnapshot.cs`.

**`Assets/Packages/com.scaffold.states/Runtime/State/Snapshot.cs` — add `: ISnapshot`**

Change the class declaration from:

    public class Snapshot : Map<IReference, Type, State>

to:

    public class Snapshot : Map<IReference, Type, State>, ISnapshot

No other changes. `Snapshot` carries its existing API unchanged.

**Create `Assets/Packages/com.scaffold.states/Runtime/State/DiffSnapshot.cs`**

`DiffSnapshot` is the data object produced by `DiffSnapshotHandler.Save()`. It carries the set of changed state values (`Changes`) and the full change ledger (`ChangeMap`). The ledger value type is the existing `StateChangeEvent` enum — no new enum is needed.

    using Scaffold.Maps;
    using System;

    namespace Scaffold.States
    {
        /// <summary>
        /// Snapshot produced by <see cref="DiffSnapshotHandler"/> containing only the canonical slices that changed since the last baseline.
        /// <see cref="Changes"/> holds the new state values for <see cref="StateChangeEvent.Created"/> and <see cref="StateChangeEvent.Updated"/> keys.
        /// <see cref="ChangeMap"/> holds the full ledger including <see cref="StateChangeEvent.Removed"/> tombstones.
        /// </summary>
        public sealed class DiffSnapshot : ISnapshot
        {
            public DiffSnapshot(Snapshot changes, Map<IReference, Type, StateChangeEvent> changeMap)
            {
                Changes = changes ?? throw new ArgumentNullException(nameof(changes));
                ChangeMap = changeMap ?? throw new ArgumentNullException(nameof(changeMap));
            }

            public Snapshot Changes { get; }
            public Map<IReference, Type, StateChangeEvent> ChangeMap { get; }
            public bool IsEmpty => Changes.Count == 0 && ChangeMap.Count == 0;
        }
    }

`Count` is on `BaseMap`. If unavailable, replace `Changes.Count == 0` with `!Changes.Any()` (add `using System.Linq;`). Verify by reading `Assets/Packages/com.scaffold.maps/Runtime/BaseMap.cs` before writing.

Run the quality gate after this milestone:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests


### Milestone 3 — Handlers

Create two new files. Do not modify `Store.cs` yet.

**File 1: `Assets/Packages/com.scaffold.states/Runtime/State/FullSnapshotHandler.cs`**

Stateless handler. Implements the same behavior that currently lives in `Store.SaveSnapshot` and `Store.LoadSnapshot`. The implementation is self-contained here; later in Milestone 4 `Store` will delegate to it.

`Save` iterates `scope.GetCanonicalSlices()` and builds a `Snapshot`. `Load` expects an `ISnapshot` that is a `Snapshot`; throws `InvalidOperationException` if the type is wrong. Load applies all entries via `scope.Apply`, then prunes canonical slices not in the snapshot via `scope.Remove`. The prune step needs the canonical slice list captured before applying (to avoid confusion with newly applied slices). `Reset` is a no-op.

    using System;
    using System.Collections.Generic;

    namespace Scaffold.States
    {
        /// <summary>
        /// Stateless snapshot handler that saves and loads all canonical slices in full.
        /// This is the default handler configured by <see cref="StoreBuilder"/> when none is supplied.
        /// </summary>
        public sealed class FullSnapshotHandler : ISnapshotHandler
        {
            public ISnapshot Save(IStoreSnapshotScope scope)
            {
                var snapshot = new Snapshot();
                foreach (SliceEntry entry in scope.GetCanonicalSlices())
                {
                    snapshot.Add(entry.Reference, entry.StateType, entry.State);
                }
                return snapshot;
            }

            public void Load(IStoreSnapshotScope scope, ISnapshot snapshot)
            {
                if (snapshot is not Snapshot full)
                {
                    throw new InvalidOperationException(
                        $"{nameof(FullSnapshotHandler)} requires a {nameof(Snapshot)} but received {snapshot?.GetType().Name ?? "null"}.");
                }

                // Capture current canonical keys before applying (needed for prune step below).
                IReadOnlyList<SliceEntry> current = scope.GetCanonicalSlices();

                // Apply all snapshot entries.
                foreach (var entry in full)
                {
                    scope.Apply(entry.Key.Primary, entry.Value);
                }

                // Remove canonical slices absent from the snapshot (prune).
                foreach (SliceEntry existing in current)
                {
                    if (!full.Contains(existing.Reference, existing.StateType))
                    {
                        scope.Remove(existing.Reference, existing.StateType);
                    }
                }
            }

            public void Reset()
            {
                // Stateless — no-op.
            }
        }
    }

Note on `full.Contains`: `Snapshot` inherits `Contains(TPrimary, TSecondary)` from `Map`. Verify the method is accessible by reading `Assets/Packages/com.scaffold.maps/Runtime/Map.cs` before writing.

Note on iterating `full`: `Snapshot` is a `Map<IReference, Type, State>`, so its enumerator yields `KeyValuePair<Index<IReference, Type>, Holder<State>>`. Read `Assets/Packages/com.scaffold.maps/Runtime/BaseMap.cs` and `IndexComposite.cs` to confirm the key type and how to access `Primary` and `Secondary`. If `entry.Value` is a `Holder<State>` then use `entry.Value.Value` for the state. Look at how `Store.SaveSnapshot()` currently iterates `map` (lines ~67–73 of `Store.cs`) and replicate the same iteration pattern.

**File 2: `Assets/Packages/com.scaffold.states/Runtime/State/DiffSnapshotHandler.cs`**

Stateful handler. Subscribes to `IStateEventHandler.SubscribeAny` at construction. Maintains two internal maps: a `Map<IReference, Type, StateChangeEvent>` ledger and a `Snapshot` of pending state values.

`OnStateChanged` records each canonical change: ignores aggregate callbacks (`state is not State`), upserts the ledger, updates or removes the pending state entry. `Save` drains both maps into a `DiffSnapshot`. `Load` applies changes and removals via scope then calls `Reset()`. `Reset` clears both maps.

    using Scaffold.Maps;
    using System;

    namespace Scaffold.States
    {
        /// <summary>
        /// Stateful snapshot handler that saves only canonical slices changed since the last baseline.
        /// Subscribe via <see cref="IStateEventHandler.SubscribeAny"/>; configure via <see cref="StoreBuilder"/>.
        /// Call <see cref="Reset"/> after <see cref="Store.SaveSnapshot"/> or <see cref="Store.LoadSnapshot"/> (full-snapshot operations)
        /// so the diff baseline is re-established from the post-operation store state.
        /// </summary>
        public sealed class DiffSnapshotHandler : ISnapshotHandler
        {
            private readonly Map<IReference, Type, StateChangeEvent> ledger;
            private readonly Snapshot pendingChanges;

            public DiffSnapshotHandler(IStateEventHandler eventHandler)
            {
                if (eventHandler is null) throw new ArgumentNullException(nameof(eventHandler));
                ledger = new Map<IReference, Type, StateChangeEvent>();
                pendingChanges = new Snapshot();
                eventHandler.SubscribeAny(OnStateChanged);
            }

            private void OnStateChanged(IReference reference, BaseState state, StateChangeEvent changeEvent)
            {
                if (state is not State canonicalState)
                {
                    return;
                }

                Type stateType = canonicalState.GetType();
                ledger[reference, stateType] = changeEvent;

                if (changeEvent == StateChangeEvent.Removed)
                {
                    pendingChanges.Remove(reference, stateType);
                }
                else
                {
                    pendingChanges.Set(reference, canonicalState);
                }
            }

            public ISnapshot Save(IStoreSnapshotScope scope)
            {
                var changeMap = new Map<IReference, Type, StateChangeEvent>();
                foreach (var entry in ledger)
                {
                    changeMap.Add(entry.Key.Primary, entry.Key.Secondary, entry.Value);
                }

                var changes = new Snapshot();
                foreach (var entry in pendingChanges)
                {
                    changes.Add(entry.Key.Primary, entry.Key.Secondary, entry.Value);
                }

                ledger.Clear();
                pendingChanges.Clear();
                return new DiffSnapshot(changes, changeMap);
            }

            public void Load(IStoreSnapshotScope scope, ISnapshot snapshot)
            {
                if (snapshot is not DiffSnapshot diff)
                {
                    throw new InvalidOperationException(
                        $"{nameof(DiffSnapshotHandler)} requires a {nameof(DiffSnapshot)} but received {snapshot?.GetType().Name ?? "null"}.");
                }

                foreach (var entry in diff.Changes)
                {
                    scope.Apply(entry.Key.Primary, entry.Value);
                }

                foreach (var entry in diff.ChangeMap)
                {
                    if (entry.Value == StateChangeEvent.Removed)
                    {
                        scope.Remove(entry.Key.Primary, entry.Key.Secondary);
                    }
                }

                Reset();
            }

            public void Reset()
            {
                ledger.Clear();
                pendingChanges.Clear();
            }
        }
    }

Implementation note: `pendingChanges.Remove(reference, stateType)` calls `Map<IReference, Type, State>.Remove(primary, secondary)`. Verify this method is accessible on `Snapshot` (it inherits from `Map`). If `Snapshot` does not expose `Remove` directly, access it through the base `Map` via an explicit cast.

Implementation note: `pendingChanges.Set(reference, canonicalState)` calls the typed `Set` helper on `Snapshot`. Verify it exists in `Snapshot.cs`; if not, use `pendingChanges[reference, stateType] = canonicalState` via the `Map` indexer.

Run the quality gate:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests


### Milestone 4 — Store and StoreBuilder wiring

This milestone modifies two existing files.

**`Assets/Packages/com.scaffold.states/Runtime/Store.cs`**

Step 1: Add `ISnapshotHandler` field. In the field declarations, after `mutatorRegistry`, add:

    private readonly ISnapshotHandler snapshotHandler;

Step 2: Update the constructor to accept an `ISnapshotHandler` parameter. Change:

    public Store(IStateEventHandler eventHandler, MutatorRegistry mutatorRegistry, params BaseSlice[] slices)

to:

    public Store(IStateEventHandler eventHandler, ISnapshotHandler snapshotHandler, MutatorRegistry mutatorRegistry, params BaseSlice[] slices)

Assign it: `this.snapshotHandler = snapshotHandler ?? throw new ArgumentNullException(nameof(snapshotHandler));`

Step 3: Replace `SaveSnapshot` and `LoadSnapshot` in the `#region Snapshots` section. The current implementations (both the save loop and the prune/apply logic) are removed. The new implementations are one-liners:

    public ISnapshot SaveSnapshot()
    {
        return snapshotHandler.Save(new SnapshotScope(this));
    }

    public void LoadSnapshot(ISnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        snapshotHandler.Load(new SnapshotScope(this), snapshot);
    }

Step 4: Remove `PruneCanonicalSlicesNotInSnapshot`. Its logic now lives in `FullSnapshotHandler.Load`. Delete the method.

Step 5: Keep `ApplySnapshot` unchanged — the `Scratchpad.Commit()` still calls it for mutator overlay commits. Do not remove it.

Step 6: Add the `SnapshotScope` private nested class at the bottom of `Store.cs`, before the final closing brace. The class implements `IStoreSnapshotScope`.

    private sealed class SnapshotScope : IStoreSnapshotScope
    {
        private readonly Store owner;

        public SnapshotScope(Store owner)
        {
            this.owner = owner;
        }

        public IReadOnlyList<SliceEntry> GetCanonicalSlices()
        {
            var result = new System.Collections.Generic.List<SliceEntry>();
            foreach (var entry in owner.map)
            {
                result.Add(new SliceEntry(entry.Key.Primary, entry.Key.Secondary, entry.Value.State as State));
            }
            return result;
        }

        public void Apply(IReference reference, State state)
        {
            var r = reference ?? Reference.Null;
            var t = state.GetType();
            if (owner.map.Contains(r, t))
            {
                owner.Set(r, state);
            }
            else
            {
                owner.RegisterSlice(r, state);
            }
        }

        public void Remove(IReference reference, Type stateType)
        {
            owner.UnregisterSlice(reference, stateType);
        }
    }

Note on `entry.Value.State`: `map` is a `Map<IReference, Type, Slice>`. The enumerator yields `KeyValuePair<Index<IReference, Type>, Holder<Slice>>`, so `entry.Value` is a `Holder<Slice>` and `entry.Value.Value` is the `Slice`. Then `slice.State` is `BaseState`; cast it with `slice.State as State`. Read how `Store.SaveSnapshot()` iterates `map` before writing this method, and replicate the access pattern exactly.

**`Assets/Packages/com.scaffold.states/Runtime/Builders/Store/StoreBuilder.cs`**

Step 1: Add a `snapshotHandler` field after `eventHandler`:

    private ISnapshotHandler? snapshotHandler;

Step 2: Add a builder method:

    public void AddSnapshotHandler(ISnapshotHandler snapshotHandler)
    {
        this.snapshotHandler = snapshotHandler;
    }

Step 3: Update `Build()` to supply the handler and update the `Store` constructor call:

    public Store Build()
    {
        IStateEventHandler stateHandler = eventHandler ?? GetDefaultStateEventHandler();
        ISnapshotHandler handler = snapshotHandler ?? new FullSnapshotHandler();
        return new Store(stateHandler, handler, mutatorRegistry ?? new MutatorRegistry(), entries.ToArray());
    }

Run the quality gate:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests

If any existing test constructs `Store` directly (not via `StoreBuilder`), update those call sites to pass a `new FullSnapshotHandler()` as the second argument. Read `DeferredStateEventHandlerTests.cs` and `StoreFeaturesSampleTests.cs` to check construction patterns before editing.


### Milestone 5 — Tests

Add `Assets/Packages/com.scaffold.states/Tests/DiffSnapshotTests.cs` and its `.meta`. Define minimal inline `record` state types rather than depending on samples.

A store for tests is built via `StoreBuilder`. For tests involving `DiffSnapshotHandler`, the wiring is:

    var events = StateEventHandlers.CreateDefault();
    var diffHandler = new DiffSnapshotHandler(events);
    var builder = new StoreBuilder();
    builder.AddEventHandler(events);
    builder.AddSnapshotHandler(diffHandler);
    // add slices...
    var store = builder.Build();

**Test 1 — Only mutated slices in diff.**
Build a store with slices A and B (via `AddState`). Attach `DiffSnapshotHandler`. Mutate A via a mutator. Call `handler.Save()`. Cast the result to `DiffSnapshot`. Assert `Changes` contains A's new value. Assert `ChangeMap` has one entry (A) with `StateChangeEvent.Updated`. B is absent from both maps.

**Test 2 — RegisterSlice appears as Created.**
After building the store, call `store.RegisterSlice(null, new SomeState(...))`. Assert `ChangeMap` has one entry with `StateChangeEvent.Created`.

**Test 3 — UnregisterSlice appears as Removed.**
Register a slice via `AddState`. Unregister it via `store.UnregisterSlice`. Call `handler.Save()`. Assert `ChangeMap` has one `Removed` entry. Assert `Changes.Count == 0`.

**Test 4 — Load applies only changed entries.**
Store A and B. Mutate A. Save diff. Build a second store with A (old value) and B. Configure it with a fresh `DiffSnapshotHandler`. Call `diffHandler2.Load(secondStore's scope... wait — load goes through Store)`. Actually: call `store2.LoadSnapshot(diff)` with the second store configured with a matching `DiffSnapshotHandler`. Assert A holds the new value; B is unchanged.

Implementation note on Test 4: `store2.LoadSnapshot(diff)` will route to `diffHandler2.Load(scope2, diff)`. The handler must accept a `DiffSnapshot` (it does). The test confirms only A changed.

**Test 5 — Load removes tombstoned slices.**
Store A and B. Unregister B. Save diff. Build second store with A and B. Load diff via second store. Assert `store2.Get<BState>()` throws `KeyNotFoundException`.

**Test 6 — Repeated writes collapse to one ledger entry.**
Mutate the same slice three times. Save. Assert `ChangeMap` has one entry; `Changes` holds the latest value.

**Test 7 — Reset clears the ledger.**
Mutate a slice. Call `handler.Reset()`. Save. Assert `diff.IsEmpty`.

**Test 8 — Reset after full-snapshot operation.**
Mutate A. Call `store.SaveSnapshot()` (full — goes through `FullSnapshotHandler`, fires no Notify calls during save). Call `handler.Reset()`. Mutate A again with a different value. Save diff. Assert only the second value appears. (This confirms the baseline was reset; the first mutation was cleared.)

Note: `store.SaveSnapshot()` uses `FullSnapshotHandler`, not `DiffSnapshotHandler`. The diff handler is not the registered handler here — this test needs a store built without `AddSnapshotHandler` (uses `FullSnapshotHandler`) while a `DiffSnapshotHandler` is attached separately via events. The two handlers can coexist: `FullSnapshotHandler` is the Store's configured handler; `DiffSnapshotHandler` is a side observer. Actually this requires careful test setup — either build two stores, or build with `DiffSnapshotHandler` as the Store's handler and call `SaveSnapshot()` (which routes to the diff handler) and verify the result. Revise the test to use a store that has `DiffSnapshotHandler` as the registered handler, do a full save first, reset manually, then mutate and save a diff.

**Test 9 — IsEmpty on fresh handler.**
Build store, attach diff handler, do not mutate anything. Save. Assert `diff.IsEmpty`.

**Test 10 — FullSnapshotHandler round-trip regression.**
Build a store with several slices. `SaveSnapshot()` returns `ISnapshot`; cast to `Snapshot`. Mutate all slices. `LoadSnapshot((ISnapshot)snapshot)` restores them. Assert all values match the original snapshot. This confirms `FullSnapshotHandler` and the `SnapshotScope` behave identically to the old `Store` logic.

Run tests with:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\run-editmode-tests.ps1"

If Unity is unavailable, the compile-only gate is the minimum bar.


### Milestone 6 — Documentation, quality gate, and commit

**Documentation:** Update `Assets/Packages/com.scaffold.states/README.md`. Add or expand a Snapshots section:

- The `ISnapshotHandler` design: one interface, swappable strategies. `FullSnapshotHandler` is the default. `DiffSnapshotHandler` is for incremental saves.
- The `DiffSnapshotHandler` wiring pattern (same event handler instance passed to both `AddEventHandler` and `DiffSnapshotHandler`'s constructor).
- The Reset contract: callers must call `handler.Reset()` after `store.SaveSnapshot()` or `store.LoadSnapshot()` when a `DiffSnapshotHandler` is in use alongside a full-snapshot operation.
- A short pseudocode example (full save baseline, then repeated diff saves).
- A forward note that the log/mutation-replay handler will implement `ISnapshotHandler` as a future strategy.

**Quality gate:**

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests

Check XML doc comments on all new public types. Existing public types in this assembly carry `<summary>` tags; add them to all new types and interface members.

**Commit:** Reference `Plans/DiffSnapshot/DiffSnapshot-ExecPlan.md`.


## Concrete Steps

All from `C:\Unity\Scaffold` (PowerShell).

Milestone 1:

    1. Create ISnapshot.cs + .meta in Runtime/Abstractions/.
    2. Create SliceEntry.cs + .meta in Runtime/State/.
    3. Create IStoreSnapshotScope.cs + .meta in Runtime/Abstractions/.
    4. Create ISnapshotHandler.cs + .meta in Runtime/Abstractions/.
    5. powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests
       Expected: PASS.

Milestone 2:

    6. Edit Runtime/State/Snapshot.cs: add ISnapshot to declaration.
    7. Create DiffSnapshot.cs + .meta in Runtime/State/.
    8. powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests
       Expected: PASS.

Milestone 3:

    9.  Create FullSnapshotHandler.cs + .meta in Runtime/State/.
    10. Create DiffSnapshotHandler.cs + .meta in Runtime/State/.
    11. powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests
        Expected: PASS.

Milestone 4:

    12. Edit Store.cs: add field, update constructor, replace SaveSnapshot/LoadSnapshot, remove PruneCanonicalSlicesNotInSnapshot, add SnapshotScope nested class.
    13. Edit StoreBuilder.cs: add field, AddSnapshotHandler method, update Build().
    14. Fix any direct Store construction call sites in tests if needed.
    15. powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests
        Expected: PASS.

Milestone 5:

    16. Create DiffSnapshotTests.cs + .meta in Tests/.
    17. powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\run-editmode-tests.ps1"
        Expected: all tests pass.
    18. If Unity unavailable: validate-changes.ps1 -SkipTests as minimum bar.

Milestone 6:

    19. Edit README.md.
    20. powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests
        Expected: PASS.
    21. git add -A && git commit.


## Validation and Acceptance

**Acceptance A — FullSnapshotHandler regression.**
`store.SaveSnapshot()` cast to `Snapshot` and `store.LoadSnapshot(snapshot)` behave identically to the pre-ExecPlan behavior. All tests in `StoreFeaturesSampleTests.cs` pass.

**Acceptance B — Diff save captures only changes.**
With N slices of which K were mutated, `(DiffSnapshot)handler.Save(...)` has `Changes.Count == K` and `ChangeMap.Count == K`.

**Acceptance C — Diff load is isolated.**
After loading a diff that modified slice A into a store containing A and B, `store.Get<B>()` returns the same value it had before the load.

**Acceptance D — Tombstones are applied on load.**
After loading a diff with one `Removed` entry, `store.Get<RemovedType>()` throws `KeyNotFoundException`.

**Acceptance E — Reset clears baseline.**
After `handler.Reset()`, `(DiffSnapshot)handler.Save(...)).IsEmpty` is `true`.

**Acceptance F — Quality gate clean.**
`validate-changes.ps1 -SkipTests` exits with code 0.


## Idempotence and Recovery

All new files are additive. To roll back: delete the new files and revert `Store.cs`, `Snapshot.cs`, and `StoreBuilder.cs`. The pre-change behavior is restored. No data migrations or destructive operations are involved. If Unity duplicates GUIDs from hand-authored `.meta` files, delete the affected `.meta` files and reimport in the Unity Editor.


## Artifacts and Notes

Pseudocode illustrating the caller experience with default and diff handlers.

    // --- Default (FullSnapshotHandler) ---
    var store = new StoreBuilder().AddState(new PlayerState()).Build();
    ISnapshot baseline = store.SaveSnapshot();  // returns Snapshot
    // ... mutations ...
    store.LoadSnapshot(baseline);               // restores all slices

    // --- Diff handler ---
    var events = StateEventHandlers.CreateDefault();
    var diffHandler = new DiffSnapshotHandler(events);
    var store = new StoreBuilder()
        .AddEventHandler(events)
        .AddSnapshotHandler(diffHandler)
        .AddState(new PlayerState())
        .Build();

    // Take a full baseline first using FullSnapshotHandler on a separate store,
    // OR just acknowledge that the diff baseline starts from store construction.

    // Each tick:
    DiffSnapshot diff = (DiffSnapshot)store.SaveSnapshot();
    if (!diff.IsEmpty)
        Serialize(diff);

    // Restore on another store configured the same way:
    store2.LoadSnapshot(diff);  // routes to DiffSnapshotHandler.Load

    // After any full-snapshot operation mixed in:
    store.LoadSnapshot(fullBaseline);  // routes through FullSnapshotHandler (if configured on store)
    diffHandler.Reset();               // clear events fired during LoadSnapshot


## Interfaces and Dependencies

By end of implementation the following must exist. Exact names are fixed; changes require a Decision Log entry.

In `Runtime/Abstractions/ISnapshot.cs`:

    public interface ISnapshot { }

In `Runtime/State/SliceEntry.cs`:

    public sealed record SliceEntry(IReference Reference, Type StateType, State State);

In `Runtime/Abstractions/IStoreSnapshotScope.cs`:

    public interface IStoreSnapshotScope
    {
        IReadOnlyList<SliceEntry> GetCanonicalSlices();
        void Apply(IReference reference, State state);
        void Remove(IReference reference, Type stateType);
    }

In `Runtime/Abstractions/ISnapshotHandler.cs`:

    public interface ISnapshotHandler
    {
        ISnapshot Save(IStoreSnapshotScope scope);
        void Load(IStoreSnapshotScope scope, ISnapshot snapshot);
        void Reset();
    }

In `Runtime/State/DiffSnapshot.cs`:

    public sealed class DiffSnapshot : ISnapshot
    {
        public DiffSnapshot(Snapshot changes, Map<IReference, Type, StateChangeEvent> changeMap);
        public Snapshot Changes { get; }
        public Map<IReference, Type, StateChangeEvent> ChangeMap { get; }
        public bool IsEmpty { get; }
    }

In `Runtime/State/FullSnapshotHandler.cs`:

    public sealed class FullSnapshotHandler : ISnapshotHandler
    {
        public ISnapshot Save(IStoreSnapshotScope scope);
        public void Load(IStoreSnapshotScope scope, ISnapshot snapshot);  // accepts Snapshot
        public void Reset();  // no-op
    }

In `Runtime/State/DiffSnapshotHandler.cs`:

    public sealed class DiffSnapshotHandler : ISnapshotHandler
    {
        public DiffSnapshotHandler(IStateEventHandler eventHandler);
        public ISnapshot Save(IStoreSnapshotScope scope);   // returns DiffSnapshot
        public void Load(IStoreSnapshotScope scope, ISnapshot snapshot);  // accepts DiffSnapshot
        public void Reset();
    }

In `Runtime/Store.cs`, updated public snapshot API:

    public ISnapshot SaveSnapshot();           // was: Snapshot — breaking signature change
    public void LoadSnapshot(ISnapshot snapshot);  // was: LoadSnapshot(Snapshot) — breaking change

In `Runtime/Builders/Store/StoreBuilder.cs`:

    public void AddSnapshotHandler(ISnapshotHandler snapshotHandler);
    // Build() defaults to new FullSnapshotHandler() when none supplied


## Revision history

- 2026-04-07: Initial ExecPlan authored. Plan had SnapshotTracker on Store, SliceChangeKind enum, SaveDiffSnapshot/LoadDiffSnapshot on Store.
- 2026-04-07 (revision 2): Drop SliceChangeKind; use StateChangeEvent. Move diff logic to external DiffSnapshotHandler subscribing to events. Store gains only MergeSnapshot. ISnapshotHandler<TSnapshot> generic interface added.
- 2026-04-07 (revision 3): Full architectural pivot per stakeholder feedback. Store holds injected ISnapshotHandler and delegates SaveSnapshot/LoadSnapshot entirely. IStoreSnapshotScope introduced as the per-call access object passed to handlers. FullSnapshotHandler extracts existing full-save/full-load logic from Store. SnapshotTracker removed; DiffSnapshotHandler owns its ledger directly. ISnapshotHandler is non-generic; Store.SaveSnapshot() now returns ISnapshot (breaking change). All snapshot logic leaves Store except the private ApplySnapshot used by Scratchpad.
