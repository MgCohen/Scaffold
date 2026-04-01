# com.scaffold.liveops

# Core LiveOps

## TL;DR

- Purpose: typed client for the deployed Cloud Code **LiveOps** module using shared DTO requests and responses; **bootstrap** runs an initial **`GameDataRequest`** via `IAsyncLayerInitializable` on `LiveOpsService`.
- Location: `Assets/Packages/com.scaffold.liveops/Runtime/` (`Scaffold.LiveOps`), installer `Scaffold.LiveOps.Container`.
- Depends on: `Scaffold.CloudCode`, `Scaffold.Scope` (for `IAsyncLayerInitializable`), `Scaffold.Ugs` (optional `IUgs` gate before Cloud Code), precompiled plugin `Scaffold.LiveOps.DTO.dll` (see the **LiveOps** section below), `Newtonsoft.Json`, `VContainer`.
- Used by: bootstrap, feature modules (`IGameClientModule` implementations, `GameClientModuleBase<T>`), and any code that calls LiveOps endpoints.

## Responsibilities

- `ILiveOpsService` / `LiveOpsService`: `CallAsync`, `GetModuleData<T>()` (reads from the last successful initial `GameDataRequest` stored on the service).
- After each `CallAsync`, an internal `ModuleResponseDispatchService` considers only the **direct** entries in `ModuleResponse.Responses` on the returned root (no deeper traversal), resolves `IEnumerable<IResponseHandler>` from `IObjectResolver` at dispatch time, and invokes handlers whose `HandledResponseType` matches each item’s runtime type (see `IResponseHandler` / `IResponseHandler<T>`).
- `LiveOpsService` implements `IAsyncLayerInitializable`: performs the initial `GameDataRequest` and stores aggregated `GameData` internally. It does not coordinate other services; callers use `GetModuleData<T>()` when their layer runs after LiveOps has initialized.
- `GameClientModuleBase<T>` implements `IAsyncLayerInitializable`: resolves `ILiveOpsService` and assigns `protected data` from `GetModuleData<T>()`. Bootstrap layer ordering should run `LiveOpsService` before these modules when they need `GameData` populated. `IGameClientModule` exposes `Key` only; typed payload lives on the concrete type as `protected T data`.
- Payload shape `{ "request": <serialized ModuleRequest> }` for Cloud Code bindings.

## Public API

| Symbol | Purpose |
|--------|---------|
| `IGameClientModule` | Client module contract: `Key` only. |
| `GameClientModuleBase<T>` | Base class with `Key` defaulting to `typeof(T).Name`, `InitializeAsync` loading from `ILiveOpsService`. |
| `ILiveOpsService.CallAsync<TResponse>` | Generic module call. |
| `ILiveOpsService.GetModuleData<T>` | Typed slice of aggregated `GameData` after initial fetch. |
| `IResponseHandler` / `IResponseHandler<T>` | Optional handler for nested `ModuleResponse` items; `HandledResponseType` selects the concrete nested type. |
| `LiveOpsService` | Implements `ILiveOpsService` and `IAsyncLayerInitializable`. |

## Registration

`LiveOpsInstaller` registers `LiveOpsService` as `ILiveOpsService` and `IAsyncLayerInitializable` (scoped). Runs from **`BootstrapCoreInstaller`** alongside other installers (for example `AdsInstaller`). Infra layer registers UGS and Cloud Code on the parent scope.

Register each concrete handler with `AsImplementedInterfaces()` so `IResponseHandler` and `IResponseHandler<T>` are both registered (for example `builder.Register<MyHandler>(Lifetime.Scoped).AsImplementedInterfaces()` or `builder.RegisterInstance(handler).AsImplementedInterfaces()`). Multiple handlers for the same nested response type are all invoked. Dispatch resolves the handler collection from the current scope’s `IObjectResolver` when a response is handled, which avoids constructor ordering issues between `LiveOpsService` and handler registration.

Register concrete feature modules as `IGameClientModule` and `IAsyncLayerInitializable` when they should hydrate during bootstrap (see `AdsInstaller`). `LiveOpsService` does not enumerate client modules; it only stores `GameData` from the initial `GameDataRequest`.

## Tests

EditMode: `Assets/Packages/com.scaffold.liveops/Tests` (`LiveOpsInitializationTests`, `GameClientModuleBaseTests`).


---

# LiveOps

Cloud Code backend under `LiveOps/` (Unity repo root): **DTO** (`LiveOps.DTO/`) and **main** module (`Project/`). Unity consumes the precompiled **`Scaffold.LiveOps.DTO.dll`** plugin (Newtonsoft.Json types and `GameModuleDTO.*` contracts).

## Layout

| Part | Path | Role |
|------|------|------|
| **DTO** | `LiveOps/LiveOps.DTO/` | Contracts (`GameModuleDTO.*` in source); project file **`Scaffold.LiveOps.DTO.csproj`**, assembly name **`Scaffold.LiveOps.DTO`** |
| **Main** | `LiveOps/Project/` | Cloud Code host (`GameModule.*`), `net6.0`, output assembly **`LiveOps.dll`** |

Build the shared contracts with **`LiveOps/LiveOps.sln`**. The DTO project copies **`Scaffold.LiveOps.DTO.dll`** (and `.pdb` when present) to **`Assets/Plugins/Scaffold.LiveOps.DTO/`** after each build (`CopyDtoToUnityPlugins` target in `Scaffold.LiveOps.DTO.csproj`). The Cloud Code host under **`LiveOps/Project/`** is built with your Unity Cloud Code / deployment pipeline when applicable.

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
