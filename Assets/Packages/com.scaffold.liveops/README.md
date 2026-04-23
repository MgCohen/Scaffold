# com.scaffold.liveops

# Core LiveOps

## TL;DR

- Purpose: typed client for the deployed Cloud Code **LiveOps** module using shared DTO requests and responses; **bootstrap** runs an initial **`GameDataRequest`** via `Scaffold.AppFlow.IAsyncInitializable` on `LiveOpsService`.
- Location: `Assets/Packages/com.scaffold.liveops/Runtime/` (`Scaffold.LiveOps`), installer `Scaffold.LiveOps.Container`.
- Depends on: `Scaffold.CloudCode`, `com.scaffold.appflow` (`Scaffold.AppFlow.IAsyncInitializable`), precompiled plugin `Scaffold.LiveOps.DTO.dll` (see the **LiveOps** section below), `Newtonsoft.Json`, `VContainer`.
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

Backend (Cloud Code host): `LiveOps/Project.Tests` — prefetch key union and `DataCacheExtensions` (`dotnet test`).


---

# LiveOps

Cloud Code backend under `LiveOps/` (Unity repo root): **DTO** (`LiveOps.DTO/`) and **main** module (`Project/`). Unity consumes the precompiled **`Scaffold.LiveOps.DTO.dll`** plugin (Newtonsoft.Json types and `GameModuleDTO.*` contracts).

## Layout

| Part | Path | Role |
|------|------|------|
| **DTO** | `LiveOps/LiveOps.DTO/` | Contracts (`GameModuleDTO.*` in source); project file **`Scaffold.LiveOps.DTO.csproj`**, assembly name **`Scaffold.LiveOps.DTO`** |
| **Main** | `LiveOps/Project/` | Cloud Code host (`GameModule.*`), `net6.0`, output assembly **`LiveOps.dll`** |

Build the shared contracts with **`LiveOps/LiveOps.sln`**. The DTO project copies **`Scaffold.LiveOps.DTO.dll`** (and `.pdb` when present) to **`Assets/Plugins/Scaffold.LiveOps.DTO/`** after each build (`CopyDtoToUnityPlugins` target in `Scaffold.LiveOps.DTO.csproj`). The Cloud Code host under **`LiveOps/Project/`** is built with your Unity Cloud Code / deployment pipeline when applicable.

### Cloud Code data pipeline (backend)

- **GameApi** (`GameApiDispatcher.Invoke`): Resolves `handler.PlayerKeys()` and `handler.ConfigKeys()` (default `null` = warm full player + full remote config snapshot). Runs `Task.WhenAll(player.WarmupAsync(...), remoteConfig.WarmupAsync(...))`, then `await using` **`BeginBatch()`** on **player** and **game state**. Inside the batch, **`Set`** updates cache only; disposing the outermost batch calls **`FlushAsync`**, which batch-writes **all** dirty keys (never a single-key partial flush).
- **`IGameApiHandler`** / **`IGameModule`**: Optional `PlayerKeys()` / `ConfigKeys()` with default `null`. **`null`** = full warm for that system; **empty array** = skip prefetch (lazy on first read); non-empty key lists are reserved for future selective fetch (today they still trigger a full snapshot until `FetchData(keys)` is implemented).
- **`GameDataHandler`** unions all registered **`IGameModule`** key hints; any module returning **`null`** for a dimension forces a full warm for that dimension.
- **`DataCacheExtensions`** (`LiveOps/Project/Core/ModuleFetchData/DataCacheExtensions.cs`): `Get` / `Set` / `GetOrSet` for **`IGameModuleData`** so **`IReadableDataCache`** / **`IWriteableDataCache`** stay free of DTO-generic methods.
- **Direct Cloud Code** **`GameDataRequest`** path (`GameModulesController`) applies the same warmup + dual batch scope before module `Initialize` and **`ModuleRequestHandler.ResolveResponse`** (which no longer calls **`SaveCache`**; persistence is owned by the batch dispose / **`FlushAsync`**).

Backend unit tests: **`LiveOps/Project.Tests`** (xUnit, `net8.0` test host) — run with `dotnet test LiveOps/Project.Tests/LiveOps.Project.Tests.csproj`.

## Unity plugins

Paths are **repo-root relative** (DTO `.csproj` lives at `LiveOps/LiveOps.DTO/`, so `..\..\Assets\Plugins\Scaffold.LiveOps.DTO\` resolves correctly regardless of the Unity project folder name on disk).

Manual copy is only needed if you build outside MSBuild or disable the post-build target:

- `LiveOps\LiveOps.DTO\bin\Release\netstandard2.1\Scaffold.LiveOps.DTO.dll` → `Assets\Plugins\Scaffold.LiveOps.DTO\`

## Build commands

```powershell
dotnet build "LiveOps\LiveOps.sln" -c Release
```

Optional manual copy:

```powershell
Copy-Item "LiveOps\LiveOps.DTO\bin\Release\netstandard2.1\Scaffold.LiveOps.DTO.dll" "Assets\Plugins\Scaffold.LiveOps.DTO\Scaffold.LiveOps.DTO.dll" -Force
```

Deploy the **LiveOps** Cloud Code module (dashboard name should match what the client uses, e.g. `"LiveOps"`). Remote config is loaded from the configured HTTP or UGS Remote Config source only; there is no on-disk JSON fallback in the module.

`LiveOps/Directory.Build.props` disables repository Roslyn analyzers for these projects.

## Unity client

Use **`ILiveOpsService`** / **`LiveOpsService`** (`Scaffold.LiveOps`, see **Core LiveOps** above) for typed **`ModuleRequest` / `ModuleResponse`** calls, or call **`ICloudCodeService`** directly. Shared contracts ship in **`Scaffold.LiveOps.DTO.dll`** (`GameModuleDTO.*` namespaces).
