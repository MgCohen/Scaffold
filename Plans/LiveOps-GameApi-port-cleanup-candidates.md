# LiveOps GameApi port — follow-up cleanup candidates

This list records **legacy or redundant artifacts** left in place after porting the core `GameApi` stack from Gear-Engine. **Do not delete** until handlers and clients are fully migrated and verified.

## Cloud Code (LiveOps server project)

| Item | Path / notes |
|------|----------------|
| Legacy game-data endpoint | [`LiveOps/Project/Core/GameModule/GameModulesController.cs`](../LiveOps/Project/Core/GameModule/GameModulesController.cs) — `[CloudCodeFunction(nameof(GameDataRequest))]` superseded by `GameApiDispatcher` (`GameApi`). |
| Legacy request pipeline | [`LiveOps/Project/Core/Response/ModuleRequestHandler.cs`](../LiveOps/Project/Core/Response/ModuleRequestHandler.cs) — superseded by `IGameApiHandler` + `GameApiSession`. |
| Per-module Cloud Code entry points | Module services (e.g. Ads, Gold, Level, DirectPush) with `[CloudCodeFunction]` on individual methods — migrate to `IGameApiHandler` + unified `GameApi` only, then remove duplicate endpoints. **Done in Scaffold** for the template: requests use **`Type.Name`** as wire key. |
| Dual cache persistence | `AddToCache` / `SaveCache` on [`IWriteableDataCache`](../LiveOps/Project/Core/ModuleFetchData/Abstraction/IWriteableDataCache.cs) and [`UnityDataCache`](../LiveOps/Project/Core/ModuleFetchData/Implementation/Unity/UnityDataCache.cs) — remove after all writers use `FlushDirtyAsync` (or a single persistence path). |
| Example / product modules | Gold, Ads, Level, GlobalConfig, DirectPush under `LiveOps/Project/Modules/` — drop or relocate if Scaffold should stay template-only. |

## Client / docs

| Item | Notes |
|------|--------|
| Docs describing per-endpoint model | e.g. [`Assets/Packages/com.scaffold.liveops/README.md`](../Assets/Packages/com.scaffold.liveops/README.md), [`Plans/Export/server-events.md`](Export/server-events.md) — align with unified `GameApi` and **`RequestKey` = request `Type.Name`**. |

## Validation note

`validate-changes.ps1` may report SCA diagnostics and occasional `CS0006` metadata errors on full `Scaffold.sln` builds depending on build graph order; Unity compilation precheck in that script passed for this port.
