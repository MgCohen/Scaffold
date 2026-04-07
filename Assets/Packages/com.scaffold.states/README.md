# Scaffold States

Runtime slice/store pattern for immutable-ish game state: **`Store`** composes **`Slice`** instances (each binds an `IReference` and a **`State`**), applies **`Mutator<TState>`** updates, notifies subscribers via **`IStateEventHandler`**, and supports **`Snapshot`** save/load.

## Dependencies

- **`Scaffold.Maps`**: typed map used by `Store` to index slices.
- **`Scaffold.Records`**: required by the Maps assembly in this project.

## Layout

- **`Runtime/Store.cs`**: central API (`Execute`, `Get`, `Subscribe`, snapshots).
- **`Runtime/State/`**: `Slice.cs`, `State.cs`, `Snapshot.cs`, `StateChangeEvent.cs`, etc.
- **`Runtime/Builders/`**: `StoreBuilder` / state builders for composition.
- **`Runtime/Events/`**: `StateEventHandler`, `DeferredStateEventHandler`, `StateEventHandlers`, `TypedSubscription`, `Ledger`.
- **`Runtime/Mutators/Mutator.cs`**: mutation wrapper.

## Snapshots

- **`SaveSnapshot()`** copies every **canonical** slice row from the store into a new **`Snapshot`** (aggregate slices are not rows in that table; they stay derived from canonical state).
- **`LoadSnapshot(snapshot)`** performs a **full restore** for canonical rows: it applies every snapshot entry, then **removes** any canonical slice whose `(reference, state type)` is **not** in the snapshot. That is how runtime **`RegisterSlice`** rows disappear when you roll back to an earlier snapshot (for example entities A and B only).
- Mutator commits (`Execute`, batch payloads) use an **internal merge-only** path: they apply pending changes to existing slices **without** pruning missing rows—so a single mutation cannot strip unrelated slices.

**Subscriptions:** `Subscribe`, `SubscribeAllReferences`, and `SubscribeAny` take **`Action<..., StateChangeEvent>`** so you can distinguish **Created** (for example `RegisterSlice`), **Updated** (mutators and snapshot apply), and **Removed** (`UnregisterSlice`, snapshot prune).

## Deferred event dispatch

Many commits in one logical frame can call `IStateEventHandler.Notify` (two-argument convenience overload for `Updated`, or the three-argument overload with `StateChangeEvent`) repeatedly, which may cascade into redundant UI work. You can wrap the default handler with **`DeferredStateEventHandler`**: it forwards all `Subscribe*` calls to an inner handler and **buffers** `Notify` while a defer scope is active (`BeginDeferScope()`). Call **`Flush()`** to deliver pending notifications to the inner handler (this runs regardless of deferral depth and does not change the depth counter).

**`StateEventMergeMode`**

- **`PreserveAll`**: replay every buffered notification in order.
- **`LatestPerKey`**: at most one inner notification per `(reference, state type)`; the last state wins. Order follows first-seen keys.

Composition example:

    var inner = StateEventHandlers.CreateDefault();
    var deferred = new DeferredStateEventHandler(inner, StateEventMergeMode.LatestPerKey);
    builder.AddEventHandler(deferred);
    using (deferred.BeginDeferScope())
    {
        // mutations that would normally notify many times…
    }
    deferred.Flush();

Control surface: **`IStateEventDeferralController`** (`Flush`, `BeginDeferScope`). You can cast `store.Events` to that interface when you know the store was built with the decorator, or keep a reference to the `DeferredStateEventHandler` instance.

For **view-layer** batching of binding refresh (separate concern), see MVVM **`BindingUpdateTiming`** / deferred binding in `com.scaffold.mvvm` and `Plans/BindingDeferredUpdate/BindingDeferredUpdate-ExecPlan.md`.

This package was restored from git history (`Game/State/`, removed in commit `1782b1e`) and placed under UPM layout as `com.scaffold.states`.
