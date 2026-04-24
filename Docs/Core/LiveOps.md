# Core LiveOps

Authoritative module documentation (includes client service and backend/DTO layout): [`Assets/Packages/com.scaffold.liveops/README.md`](../../Assets/Packages/com.scaffold.liveops/README.md).

## Backend note

The Cloud Code host (`LiveOps/Core/LiveOps.Core/` + `LiveOps/Deploy/LiveOps/` + `LiveOps/Modules/LiveOps.Modules/`) batches player and game-state writes per request: **`WarmupAsync`** (parallel player + remote config), **`BeginBatch`** on player and game state, cache-only **`Set`** inside the batch, then **`FlushAsync`** on dispose. Handlers expose optional **`PlayerKeys()`** / **`ConfigKeys()`**; **`DataCacheExtensions`** use **`KeyOf<T>.Module`**, backed by **`[LiveOpsKey]`** on DTOs (snapshot types still use **`IGameModuleData`** as a marker only; persistence and config do not implement it). **`ModuleConfig`** (`ICloudCodeSetup`) registers core services; handler and **IGameModule** types are registered from the build-generated manifest via **`LiveOpsBootstrapper.InstallFromManifest`**. See the README section **Cloud Code data pipeline (backend)** and [LiveOpsKeys.md](LiveOpsKeys.md).

### GameApi wire and DTOs

- **`RequestKey`** on the **`GameApiEnvelopeRequest`** (single Cloud Code entry point) is **`KeyOf.WireOf(requestType)`** (by default, **`TRequest`.Name**). **`GameApiRegistry`** maps request types to handlers; it throws if two request types share the same resolved wire key.

### Modules and manifest

- **`IGameModule.InitializeAsync(GameApiSession, CancellationToken)`** loads module data from caches on the per-request session.
- **`ModuleConfig`** calls **`LiveOpsBootstrapper.InstallFromManifest`** from the **`Scaffold.LiveOps.Bootstrap.Generators`** build (analyzer reference on **`LiveOps/Deploy/LiveOps`**). If **`LiveOpsManifest.Entries`** is empty, the host **throws** at startup so misconfigured or generator-skipped builds fail fast; there is no reflection-based fallback.

### Auth

- Direct push and similar flows use DI **`IServerAuth`** (constant-time compare) instead of a static key type.
