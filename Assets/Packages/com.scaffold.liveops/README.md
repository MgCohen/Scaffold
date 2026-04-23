# com.scaffold.liveops

# Core LiveOps

## TL;DR

- Purpose: typed client for the deployed Cloud Code **LiveOps** module using shared DTO requests and responses; **bootstrap** runs an initial **`GameDataRequest`** via `Scaffold.AppFlow.IAsyncInitializable` on `LiveOpsService`.
- Location: `Assets/Packages/com.scaffold.liveops/Runtime/` (`Scaffold.LiveOps`), installer `Scaffold.LiveOps.Container`.
- Depends on: `Scaffold.CloudCode`, `com.scaffold.appflow` (`Scaffold.AppFlow.IAsyncInitializable`), precompiled plugins **`Scaffold.LiveOps.Core.DTO.dll`** and **`Scaffold.LiveOps.Modules.DTO.dll`** (see the **LiveOps** section below), `Newtonsoft.Json`, `VContainer`.
- Used by: bootstrap, feature modules (`IGameClientModule` implementations, `GameClientModuleBase<T>`), and any code that calls LiveOps endpoints.

## Responsibilities

- `ILiveOpsService` / `LiveOpsService`: `CallAsync`, `GetModuleData<T>()` (reads from the last successful initial `GameDataRequest` stored on the service). **`LiveOpsService`** depends on **`CloudCodeOptimisticHandlerRegistry`** and **`CloudCodeErrorHandler`** (from **`CloudCodeInstaller`**) for optional **GameApi** optimistic responses; register **`CloudCodeInstaller`** before or with **`LiveOpsInstaller`** so those singletons exist.
- After each `CallAsync`, an internal `ModuleResponseDispatchService` considers only the **direct** entries in `ModuleResponse.Responses` on the returned root (no deeper traversal), resolves `IEnumerable<IResponseHandler>` from `Scaffold.AppFlow.ILayerResolver` (current top scope) on first dispatch and caches the list, and invokes handlers whose `HandledResponseType` matches each item’s runtime type (see `IResponseHandler` / `IResponseHandler<T>`).
- `LiveOpsService` implements `Scaffold.AppFlow.IAsyncInitializable`: performs the initial `GameDataRequest` and stores aggregated `GameData` internally. It does not coordinate other services; callers use `GetModuleData<T>()` when their layer runs after LiveOps has initialized.
- `GameClientModuleBase<T>` implements `Scaffold.AppFlow.IAsyncInitializable`: constructor-injects `ILiveOpsService` and assigns `protected data` from `GetModuleData<T>()`. Bootstrap layer ordering should run `LiveOpsService` before these modules when they need `GameData` populated. `IGameClientModule` exposes `Key` only; typed payload lives on the concrete type as `protected T data`; subclasses use `protected liveOps` for `CallAsync` and other operations.
- Payload shape `{ "request": <serialized ModuleRequest> }` for Cloud Code bindings.

## Public API

| Symbol | Purpose |
|--------|---------|
| `IGameClientModule` | Client module contract: `Key` only. |
| `GameClientModuleBase<T>` | Base class with `Key` defaulting to `typeof(T).Name`, `InitializeAsync` loading from `ILiveOpsService`. |
| `ILiveOpsService.CallAsync<TResponse>` | Generic module call. |
| `ILiveOpsService.GetModuleData<T>` | Typed slice of aggregated `GameData` after initial fetch. |
| `IResponseHandler` / `IResponseHandler<T>` | Optional handler for nested `ModuleResponse` items; `HandledResponseType` selects the concrete nested type. |
| `LiveOpsService` | Implements `ILiveOpsService` and `Scaffold.AppFlow.IAsyncInitializable`. |

## Registration

`LiveOpsInstaller` registers `LiveOpsService` as `ILiveOpsService` and `Scaffold.AppFlow.IAsyncInitializable` (scoped). Register it from your application composition root alongside other installers (for example [`CurrencyClientInstaller`](../../GearEngine/Scripts/Game/Campaign/Bootstrap/Currency/CurrencyClientInstaller.cs) when you use currency client modules). UGS and Cloud Code should be registered on the same main scope per your startup plan.

Register each concrete handler with `AsImplementedInterfaces()` so `IResponseHandler` and `IResponseHandler<T>` are both registered (for example `builder.Register<MyHandler>(Lifetime.Scoped).AsImplementedInterfaces()` or `builder.RegisterInstance(handler).AsImplementedInterfaces()`). Multiple handlers for the same nested response type are all invoked. Dispatch resolves the handler collection from `ILayerResolver` (current top scope) on first nested dispatch, which avoids constructor ordering issues between `LiveOpsService` and handler registration.

Register concrete feature modules as `IGameClientModule` and `IAsyncInitializable` when they should hydrate during bootstrap (see [`LiveOpsLayer`](../../GearEngine/Scripts/App/Bootstrap/Layers/LiveOpsLayer.cs) and the `Campaign*Installer` types under `Game.Campaign`). `LiveOpsService` does not enumerate client modules; it only stores `GameData` from the initial `GameDataRequest`.

## Tests

EditMode: `Assets/Packages/com.scaffold.liveops/Tests` (`LiveOpsInitializationTests`, `GameClientModuleBaseTests`).

Backend (Cloud Code host): `LiveOps/Tests/LiveOps.Tests` — prefetch key union and `DataCacheExtensions` (`dotnet test LiveOps/LiveOps.Tests.sln`).


---

# LiveOps

Cloud Code backend under `LiveOps/` (Unity repo root): **core** (`Core/LiveOps.Core/`), **feature modules** (`Modules/LiveOps.Modules/`), and two DTO assemblies. Unity consumes precompiled **`Scaffold.LiveOps.Core.DTO.dll`** (core contracts, `LiveOps.Core.DTO.*`) and **`Scaffold.LiveOps.Modules.DTO.dll`** (feature DTOs, `LiveOps.Modules.DTO.*`).

## Layout

| Part | Path | Role |
|------|------|------|
| **Core.DTO** | `LiveOps/Core/LiveOps.Core.DTO/` | Shared envelopes, `ModuleRequest` / `ModuleResponse`, `GameData` aggregate, JSON binder; **`Scaffold.LiveOps.Core.DTO.csproj`** |
| **Modules.DTO** | `LiveOps/Modules/LiveOps.Modules.DTO/` | Feature payloads and requests (`GameDataRequest` / `GameDataResponse`, Ads, Gold, Level, …); **`Scaffold.LiveOps.Modules.DTO.csproj`** |
| **Core** | `LiveOps/Core/LiveOps.Core/` | Cloud Code host core (`LiveOps.Core.*`): caches, GameApi, `ModuleConfig`, **`LiveOps.Core.csproj`** → **`LiveOps.Core.dll`** |
| **Modules** | `LiveOps/Modules/LiveOps.Modules/` | Feature services + `*Installer` types (`LiveOps.Modules.*`); **`LiveOps.csproj`** (UGS module name **`LiveOps`**, no dots) → **`LiveOps.Modules.dll`** |

Build **`LiveOps/LiveOps.sln`** (production: four projects only — keeps Cloud Code deploy bundle small). Each DTO project copies its DLL (+ PDB) to **`Assets/Plugins/Scaffold.LiveOps.DTO/`** after build.

**Adding a feature module:** add runtime code under `Modules/LiveOps.Modules/<Feature>/`, DTOs under `Modules/LiveOps.Modules.DTO/<Feature>/`, and a **`FeatureInstaller : GameModuleInstaller`** that calls **`RegisterModule<T>`** (and **`RegisterHandler<T>`** if you add a GameApi handler). **`ModuleConfig`** discovers concrete **`ICloudCodeInstaller`** types in assemblies whose name starts with **`LiveOps`** (after **`CoreInstaller`** runs first).

### Cloud Code data pipeline (backend)

- **GameApi** (`GameApiDispatcher.Invoke`): Resolves `handler.PlayerKeys()` and `handler.ConfigKeys()` (default `null` = warm full player + full remote config snapshot). Runs `Task.WhenAll(player.WarmupAsync(...), remoteConfig.WarmupAsync(...))`, then `await using` **`BeginBatch()`** on **player** and **game state**. Inside the batch, **`Set`** updates cache only; disposing the outermost batch calls **`FlushAsync`**, which batch-writes **all** dirty keys (never a single-key partial flush).
- **`IGameApiHandler`** / **`IGameModule`**: Optional `PlayerKeys()` / `ConfigKeys()` with default `null`. **`null`** = full warm for that system; **empty array** = skip prefetch (lazy on first read); non-empty key lists are reserved for future selective fetch (today they still trigger a full snapshot until `FetchData(keys)` is implemented).
- **`GameDataHandler`** (`LiveOps.Modules.GameData`) unions all registered **`IGameModule`** key hints; any module returning **`null`** for a dimension forces a full warm for that dimension.
- **`DataCacheExtensions`** (`Core/LiveOps.Core/ModuleFetchData/DataCacheExtensions.cs`): `Get` / `Set` / `GetOrSet` for **`IGameModuleData`** so **`IReadableDataCache`** / **`IWriteableDataCache`** stay free of DTO-generic methods.
- **`ModuleRequestHandler.ResolveResponse`** does not call **`SaveCache`**; persistence is owned by the batch dispose / **`FlushAsync`**.

Backend unit tests: **`LiveOps/Tests/LiveOps.Tests`** (xUnit) — use **`LiveOps/LiveOps.Tests.sln`** (includes test project; **not** part of the deploy **`LiveOps.sln`**).

## Unity plugins

Paths are **repo-root relative** (e.g. `LiveOps/Core/LiveOps.Core.DTO/` → `..\..\..\Assets\Plugins\Scaffold.LiveOps.DTO\`).

Manual copy is only needed if you build outside MSBuild or disable the post-build target:

- `LiveOps\Core\LiveOps.Core.DTO\bin\Release\netstandard2.1\Scaffold.LiveOps.Core.DTO.dll` → `Assets\Plugins\Scaffold.LiveOps.DTO\`
- `LiveOps\Modules\LiveOps.Modules.DTO\bin\Release\netstandard2.1\Scaffold.LiveOps.Modules.DTO.dll` → `Assets\Plugins\Scaffold.LiveOps.DTO\`

## Build commands

```powershell
dotnet build "LiveOps\LiveOps.sln" -c Release
dotnet test "LiveOps\LiveOps.Tests.sln" -c Release
```

Optional manual copy:

```powershell
Copy-Item "LiveOps\Core\LiveOps.Core.DTO\bin\Release\netstandard2.1\Scaffold.LiveOps.Core.DTO.dll" "Assets\Plugins\Scaffold.LiveOps.DTO\" -Force
Copy-Item "LiveOps\Modules\LiveOps.Modules.DTO\bin\Release\netstandard2.1\Scaffold.LiveOps.Modules.DTO.dll" "Assets\Plugins\Scaffold.LiveOps.DTO\" -Force
```

Deploy the **LiveOps** Cloud Code module (dashboard / UGS module name must be a valid identifier, e.g. **`LiveOps`** — dots in the **project file name** are rejected). Point `LiveOps.ccmr` at **`LiveOps/Modules/LiveOps.Modules/LiveOps.csproj`** (main project with **`FolderProfile`**). Remote config is loaded from the configured HTTP or UGS Remote Config source only; there is no on-disk JSON fallback in the module.

`LiveOps/Directory.Build.props` disables repository Roslyn analyzers for these projects.

## Unity client

Use **`ILiveOpsService`** / **`LiveOpsService`** (`Scaffold.LiveOps`, see **Core LiveOps** above) for typed **`ModuleRequest` / `ModuleResponse`** calls, or call **`ICloudCodeService`** directly. Shared contracts ship in **`Scaffold.LiveOps.Core.DTO.dll`** and **`Scaffold.LiveOps.Modules.DTO.dll`** (`LiveOps.Core.DTO.*` and `LiveOps.Modules.DTO.*` namespaces).
