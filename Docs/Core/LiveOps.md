# Core LiveOps

Authoritative module documentation (includes client service and backend/DTO layout): [`Assets/Packages/com.scaffold.liveops/README.md`](../../Assets/Packages/com.scaffold.liveops/README.md).

## Backend note

The Cloud Code host (`LiveOps/Core/LiveOps.Core/` + `LiveOps/Modules/LiveOps.Modules/`) batches player and game-state writes per request: **`WarmupAsync`** (parallel player + remote config), **`BeginBatch`** on player and game state, cache-only **`Set`** inside the batch, then **`FlushAsync`** on dispose. Handlers expose optional **`PlayerKeys()`** / **`ConfigKeys()`**; **`IGameModuleData`**-typed helpers live in **`DataCacheExtensions`**. Feature DI is composed via **`ICloudCodeInstaller`** + **`GameModuleInstaller`**. See the README section **Cloud Code data pipeline (backend)**.
