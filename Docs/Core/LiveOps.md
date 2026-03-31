# Core LiveOps

## TL;DR

- Purpose: typed client for the deployed Cloud Code **LiveOps** module using shared DTO requests and responses; **bootstrap** runs an initial **`GameDataRequest`** via `IAsyncLayerInitializable` on `LiveOpsService`.
- Location: `Assets/Packages/com.scaffold.liveops/Runtime/` (`Scaffold.LiveOps`), installer `Scaffold.LiveOps.Container`.
- Depends on: `Scaffold.CloudCode`, `Scaffold.Scope` (for `IAsyncLayerInitializable`), `Scaffold.Ugs` (optional `IUgs` gate before Cloud Code), precompiled plugin `Scaffold.LiveOps.DTO.dll` (see `Docs/LiveOps.md`), `Newtonsoft.Json`, `VContainer`.
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
