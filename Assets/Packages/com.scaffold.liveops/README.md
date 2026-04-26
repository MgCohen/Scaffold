# com.scaffold.liveops

# Core LiveOps

## TL;DR

- Purpose: typed client for the deployed Cloud Code **LiveOps** module using shared DTO requests and responses; **bootstrap** runs an initial `**GameDataRequest`** via `Scaffold.AppFlow.IAsyncInitializable` on `LiveOpsService`.
- Location: `Assets/Packages/com.scaffold.liveops/Runtime/` (`Scaffold.LiveOps`), installer `Scaffold.LiveOps.Container`.
- Depends on: `Scaffold.CloudCode`, `com.scaffold.appflow` (`Scaffold.AppFlow.IAsyncInitializable`), precompiled DTO plugins under `Assets/Plugins/Scaffold.LiveOps.DTO/` — `**LiveOps.DTO.dll`** plus one assembly per feature (`**Scaffold.LiveOps.*.DTO.dll`**; see **LiveOps** below), `Newtonsoft.Json`, `VContainer`.
- Used by: bootstrap, feature modules (`IGameClientModule` implementations, `GameClientModuleBase<T>`), and any code that calls LiveOps endpoints.

## Responsibilities

- `ILiveOpsService` / `LiveOpsService`: `CallAsync`, `GetModuleData<T>()` (reads from the last successful initial `GameDataRequest` stored on the service). `**LiveOpsService`** depends on `**CloudCodeOptimisticHandlerRegistry`** and `**CloudCodeErrorHandler`** (from `**CloudCodeInstaller**`) for optional **GameApi** optimistic responses; register `**CloudCodeInstaller`** before or with `**LiveOpsInstaller`** so those singletons exist.
- After each `CallAsync`, an internal `ModuleResponseDispatchService` considers only the **direct** entries in `ModuleResponse.Responses` on the returned root (no deeper traversal), resolves `IEnumerable<IResponseHandler>` from `Scaffold.AppFlow.ILayerResolver` (current top scope) on first dispatch and caches the list, and invokes handlers whose `HandledResponseType` matches each item’s runtime type (see `IResponseHandler` / `IResponseHandler<T>`).
- `LiveOpsService` implements `Scaffold.AppFlow.IAsyncInitializable`: performs the initial `GameDataRequest` and stores aggregated `GameData` internally. It does not coordinate other services; callers use `GetModuleData<T>()` when their layer runs after LiveOps has initialized.
- `GameClientModuleBase<T>` implements `Scaffold.AppFlow.IAsyncInitializable`: constructor-injects `ILiveOpsService` and assigns `protected data` from `GetModuleData<T>()`. Bootstrap layer ordering should run `LiveOpsService` before these modules when they need `GameData` populated. `IGameClientModule` exposes `Key` only; typed payload lives on the concrete type as `protected T data`; subclasses use `protected liveOps` for `CallAsync` and other operations.
- Payload shape `{ "request": <serialized ModuleRequest> }` for Cloud Code bindings.

## Public API


| Symbol                                     | Purpose                                                                                                                               |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| `IGameClientModule`                        | Client module contract: `Key` only.                                                                                                   |
| `GameClientModuleBase<T>`                  | Base class with `Key` from `KeyOf<T>.Module` (see `[LiveOpsKey]` on snapshot DTOs), `InitializeAsync` loading from `ILiveOpsService`. |
| `ILiveOpsService.CallAsync<TResponse>`     | Generic module call.                                                                                                                  |
| `ILiveOpsService.GetModuleData<T>`         | Typed slice of aggregated `GameData` after initial fetch.                                                                             |
| `IResponseHandler` / `IResponseHandler<T>` | Optional handler for nested `ModuleResponse` items; `HandledResponseType` selects the concrete nested type.                           |
| `LiveOpsService`                           | Implements `ILiveOpsService` and `Scaffold.AppFlow.IAsyncInitializable`.                                                              |


## Registration

`LiveOpsInstaller` registers `LiveOpsService` as `ILiveOpsService` and `Scaffold.AppFlow.IAsyncInitializable` (scoped). Register it from your application composition root alongside other installers (for example `[CurrencyClientInstaller](../../GearEngine/Scripts/Game/Campaign/Bootstrap/Currency/CurrencyClientInstaller.cs)` when you use currency client modules). UGS and Cloud Code should be registered on the same main scope per your startup plan.

Register each concrete handler with `AsImplementedInterfaces()` so `IResponseHandler` and `IResponseHandler<T>` are both registered (for example `builder.Register<MyHandler>(Lifetime.Scoped).AsImplementedInterfaces()` or `builder.RegisterInstance(handler).AsImplementedInterfaces()`). Multiple handlers for the same nested response type are all invoked. Dispatch resolves the handler collection from `ILayerResolver` (current top scope) on first nested dispatch, which avoids constructor ordering issues between `LiveOpsService` and handler registration.

Register concrete feature modules as `IGameClientModule` and `IAsyncInitializable` when they should hydrate during bootstrap (see `[LiveOpsLayer](../../GearEngine/Scripts/App/Bootstrap/Layers/LiveOpsLayer.cs)` and the `Campaign*Installer` types under `Game.Campaign`). `LiveOpsService` does not enumerate client modules; it only stores `GameData` from the initial `GameDataRequest`.

## Tests

EditMode: `Assets/Packages/com.scaffold.liveops/Tests` (`LiveOpsInitializationTests`, `GameClientModuleBaseTests`).

Backend (Cloud Code host): `LiveOps/Tests/LiveOps.Tests` — `dotnet test LiveOps/Tests/LiveOps.Tests/LiveOps.Tests.csproj` (or build `LiveOps/LiveOps.sln`, which includes the test project).

---

# LiveOps

Cloud Code backend under `LiveOps/`: **Deploy** (host, shared core, build, generator DLL; mirrored from `**com.scaffold.liveops/Backend~`**), Scaffold (per-feature `**Scaffold.LiveOps.*`** modules and DTOs, shipped from each feature package’s `**Backend~**`), and **Game** (consumer-only features). Unity precompiles `**LiveOps.DTO.dll`** (includes **GameData** request/response types) and per-feature `**Scaffold.LiveOps.<Feature>.DTO.dll`** into `**Assets/Plugins/Scaffold.LiveOps.DTO/`**.

## Layout


| Part                  | Path                                                                    | Role                                                                                                                                                                                           |
| --------------------- | ----------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Core.DTO**          | `LiveOps/Deploy/Core/LiveOps.DTO/`                                      | Shared envelopes, `ModuleRequest` / `ModuleResponse`, aggregated `GameData`, **GameData** wire types (`**LiveOps.Modules.DTO.GameData`**), JSON binder; `LiveOps.DTO.csproj`                   |
| **Core**              | `LiveOps/Deploy/Core/LiveOps.Core/`                                     | Server library: `GameApiRegistry`, `IGameApiHandler`, `**GameDataHandler`**, `LiveOpsBootstrapper`, `IGameSetup`                                                                               |
| **Scaffold features** | `LiveOps/Scaffold/<Feature>/` and `LiveOps/Scaffold/<Feature.DTO>/`     | One runtime + one DTO project per feature (e.g. Ads, DirectPush); each feature can live under its Unity package as `**Backend~/Scaffold/`**                                                    |
| **Game**              | `LiveOps/Game/`**                                                       | Consumer-owned modules; never overwritten by the package updater                                                                                                                               |
| **Deploy**            | `LiveOps/Deploy/LiveOps/`                                               | `GameApiDispatcher` (`[CloudCodeFunction]`), `ModuleConfig` (`ICloudCodeSetup`); `LiveOps.csproj` (assembly `LiveOps`)                                                                         |
| **Build**             | `LiveOps/Deploy/Build/`                                                 | Shared `Directory.Build` imports, DTO copy targets                                                                                                                                             |
| **Artifacts**         | `LiveOps/.artifacts/bin/<Project>/`** and `.artifacts/obj/<Project>/`** | Centralized build output (gitignored). All `LiveOps/**` projects redirect `BaseOutputPath`/`BaseIntermediateOutputPath` here via `LiveOps/Directory.Build.props` so source folders stay clean. |

### LiveOps.Deploy.sln (solution folders)

Opening `**LiveOps/LiveOps.Deploy.sln**` in Visual Studio or Rider shows a virtual tree aligned with on-disk paths:

| Solution folder | Projects |
| --- | --- |
| **Deploy** / **Core** | `LiveOps.Core`, `LiveOps.DTO` (`Deploy/Core/...`) |
| **Deploy** / **Host** | `LiveOps` host (`Deploy/LiveOps/LiveOps.csproj`) |
| **Scaffold** / *&lt;Feature&gt;* | Each `**Scaffold.LiveOps.<Feature>**` and `**Scaffold.LiveOps.<Feature>.DTO**` under `LiveOps/Scaffold/<Feature>/` and `LiveOps/Scaffold/<Feature.DTO>/` |
| **Game** / *&lt;Module&gt;* | Consumer modules under `LiveOps/Game/<Module>/` and `LiveOps/Game/<Module.DTO>/` (folders appear after those projects exist) |

**Install or Update Backend** (menu) and **`**install-liveops-backend.ps1**`** copy the template `**Backend~/LiveOps.Deploy.sln**`, prune missing `.csproj` entries, remove stale `NestedProjects` rows for pruned GUIDs, then add any discovered `LiveOps/Scaffold/**` and `LiveOps/Game/**` projects with `**dotnet sln add --solution-folder**`, using the same path mapping as `**LiveOpsBackendInstall.MapCsprojToSolutionFolder**` (kept in sync with `**.agents/scripts/install-liveops-backend.ps1**`). Re-running install **re-copies the template and re-syncs nesting**; manually edited solution-folder layout in a consumer repo is **normalized** to this structure on the next install.

Build `**LiveOps/LiveOps.sln**` (local, includes tests) or `**LiveOps/LiveOps.Deploy.sln**` (no test project; matches what `**LiveOps.ccmr**` deploys to UGS Cloud Code). DTO projects copy `**<AssemblyName>.dll**` from `**LiveOps/.artifacts/bin/<Project>/**` to `**Assets/Plugins/Scaffold.LiveOps.DTO/**` (shared MSBuild target).

**Backend `**Backend~`** flow:** in this repo, run `**pwsh -File .agents/scripts/refresh-liveops-template.ps1`** to push `**LiveOps/`** into every `**Assets/Packages/*/Backend~**` (host + feature packages), or use **Scaffold > LiveOps > Refresh Backend Template** (only in projects that define `**SCAFFOLD_LIVEOPS_PACKAGE_DEV`** for the **Editor** platform, as in this repository). For every consumer, use **Scaffold > LiveOps > Install or Update Backend** (in-package, no `**.agents**` copy required) or, from a Scaffold checkout, `**install-liveops-backend.ps1**`, to merge all `**Backend~`** trees into `**LiveOps/**` while preserving `**LiveOps/Game**`. **Scaffold > LiveOps > Backend Window** lists `**com.scaffold.*/Backend~**` packages, per-package Update/Refresh (dev), full **Update All** / **Refresh All** (dev), and **Deploy** (merge + `**ugs deploy LiveOps/LiveOps.Deploy.sln**` using the UGS CLI and linked project/environment). Step-by-step (authoring vs consumer, CLI vs menu, and AI guidance): `[Docs/LiveOps/Backend-Authoring-Guide.md](../../../Docs/LiveOps/Backend-Authoring-Guide.md)`.

**Adding a feature module (Scaffold):** add `**LiveOps/Scaffold/<Feature>/`** and `**LiveOps/Scaffold/<Feature.DTO>/`** with `**[LiveOpsKey]**` as in `**Docs/Core/LiveOpsKeys.md**`, and ship the same tree from `**Assets/Packages/com.scaffold.<feature>/Backend~/Scaffold/**`. Copy from `**Tools/BackendTemplate/com.scaffold.example**` for a minimal runnable pair of csprojs. `**Scaffold.LiveOps.Bootstrap.Generators**` drives `**LiveOpsManifest**`, `**LiveOpsKeys**`, and per-DTO-assembly `**LiveOpsKeyRuntimeMap**` (`**LOPSKEY001**` / `**LOPSKEY002**`). Rebuild the deploy project. For game-specific code, add projects under `**LiveOps/Game/**` and implement `**IGameSetup**` in the consumer assembly for extra DI (optional).

**IGameSetup:** `**LiveOps.Core`** defines `**IGameSetup.Configure(ICloudCodeConfig, GameApiRegistry)`**; `**ModuleConfig**` discovers implementations in assemblies marked with `**AssemblyMetadata("ScaffoldLiveOpsAssembly", "true")**` (MSBuild) after `**InstallFromManifest**`.

### Cloud Code data pipeline (backend)

- **GameApi** (`GameApiDispatcher.Invoke`): Resolves `handler.PlayerKeys()` and `handler.ConfigKeys()` (default `null` = warm full player + full remote config snapshot). Runs `Task.WhenAll(player.WarmupAsync(...), remoteConfig.WarmupAsync(...))`, then `await using` `**BeginBatch()`** on **player** and **game state**. Inside the batch, `**Set`** updates cache only; disposing the outermost batch calls `**FlushAsync`**, which batch-writes **all** dirty keys (never a single-key partial flush).
- `**IGameApiHandler`** / `**IGameModule`**: Optional `PlayerKeys()` / `ConfigKeys()` with default `null`. `**null`** = full warm for that system; **empty array** = skip prefetch (lazy on first read); non-empty key lists are reserved for future selective fetch (today they still trigger a full snapshot until `FetchData(keys)` is implemented).
- `**GameDataHandler`** (`LiveOps.Modules.GameData`) unions all registered `**IGameModule`** key hints; any module returning `**null`** for a dimension forces a full warm for that dimension.
- `**DataCacheExtensions**` (`Deploy/Core/LiveOps.Core/ModuleFetchData/DataCacheExtensions.cs`): generic `Get` / `Set` / `GetOrSet` using `**KeyOf<T>.Module**`, so `**IReadableDataCache**` / `**IWriteableDataCache**` stay free of DTO-specific methods and keys are driven by `**[LiveOpsKey]**` on the DTO.
- **GameApi handlers** return the primary `**ModuleResponse`** directly; nested side effects use `**GameApiSession.EmitSideEffect`**. Persistence is owned by the batch dispose / `**FlushAsync`** (not per-handler `**SaveCache**`).

Backend unit tests: `**LiveOps/Tests/LiveOps.Tests**` (xUnit) — `**dotnet test LiveOps/Tests/LiveOps.Tests/LiveOps.Tests.csproj**` (test project is also in `**LiveOps/LiveOps.sln**`).

## Unity plugins

`**LiveOps/Deploy/Build/Scaffold.LiveOps.DtoCopy.targets**` runs after DTO projects build and copies to `**Assets/Plugins/Scaffold.LiveOps.DTO/**` using each assembly name (`<AssemblyName>.dll`).

## Build commands

```powershell
dotnet build "LiveOps\LiveOps.sln" -c Release
dotnet test "LiveOps\Tests\LiveOps.Tests\LiveOps.Tests.csproj" -c Release
dotnet publish "LiveOps\Deploy\LiveOps\LiveOps.csproj" -c Release -r linux-x64 --no-self-contained
```

UGS **module name** comes from the deploy `**.csproj` base name: `**LiveOps`**. Point `**LiveOps.ccmr`** at `**LiveOps/LiveOps.Deploy.sln**` (not `**LiveOps.sln**`: the full solution includes `**LiveOps.Tests**` and bloats the Cloud Code module upload toward the 10MB limit; the `.ccmr` `**modulePath**` must end in `**.sln**`). Use `**LiveOps.sln**` for local builds and `**dotnet test LiveOps/Tests/...**`. The generator DLL for consumers is in `**LiveOps/Deploy/Tools/Generators/**` (and under `**com.scaffold.liveops/Backend~/Deploy/Tools/Generators/**` in the package).

`LiveOps/Directory.Build.props` imports shared props and disables per-repo analyzers for Cloud Code projects.

## Unity client

Use `**ILiveOpsService**` / `**LiveOpsService**` for typed `**ModuleRequest` / `ModuleResponse**`. Type namespaces remain `**LiveOps.DTO.***` and `**LiveOps.Modules.DTO.***` across the split per-feature DTO assemblies.