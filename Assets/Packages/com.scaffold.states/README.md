# Scaffold States

Runtime slice/store pattern for immutable-ish game state: **`Store`** composes **`Slice`** instances (each binds an `IReference` and a **`State`**), applies **`Mutator<TState>`** updates, notifies subscribers via **`IStateEventHandler`**, and supports **`Snapshot`** save/load.

## Dependencies

- **`Scaffold.Maps`**: typed map used by `Store` to index slices.
- **`Scaffold.Records`**: required by the Maps assembly in this project.

## Layout

- **`Runtime/Store.cs`**: central API (`Execute`, `Get`, `Subscribe`, snapshots).
- **`Runtime/State/Slice.cs`**, **`State.cs`**, **`Snapshot.cs`**: core model types.
- **`Runtime/Builders/`**: `StoreBuilder` / state builders for composition.
- **`Runtime/Events/`**: `StateEventHandler`, `Subscription`, `Ledger`.
- **`Runtime/Mutators/Mutator.cs`**: mutation wrapper.

This package was restored from git history (`Game/State/`, removed in commit `1782b1e`) and placed under UPM layout as `com.scaffold.states`.
