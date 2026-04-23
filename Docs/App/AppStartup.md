# Application startup (composition root)

There is no `com.scaffold.bootstrap` package in this repository.

Wire your game by **subclassing** [`AppFlowRoot`](../../Assets/Packages/com.scaffold.appflow/Runtime/AppFlowRoot.cs) on a root `LifetimeScope` in **your** assembly:

- Override `ConfigureApplication(IContainerBuilder)` to register root services (for example navigation holder, shared contracts).
- Implement `GetInitialLayers()` to return an ordered sequence of [`IScopeLayer`](../../Assets/Packages/com.scaffold.appflow/Runtime/Contracts/IScopeLayer.cs) instances. The host pushes each layer as a child `LifetimeScope`, runs [`IAsyncInitializable`](../../Assets/Packages/com.scaffold.appflow/Runtime/Contracts/IAsyncInitializable.cs) after each push, and supports async teardown via `IAsyncDisposable` on pop.
- Optionally override `CreateScheduler()` to supply an [`IInLayerScheduler`](../../Assets/Packages/com.scaffold.appflow/Runtime/Contracts/IInLayerScheduler.cs) for init-wave ordering.
- Resolve [`ILayerResolver`](../../Assets/Packages/com.scaffold.appflow/Runtime/Contracts/ILayerResolver.cs) from the container when you need the **current top** `IObjectResolver` (for example navigation injecting view models into the active layer).
- Use [`AppFlowRoot`](../../Assets/Packages/com.scaffold.appflow/Runtime/AppFlowRoot.cs) **`Progress`** and **`Errors`** from a loading screen (or resolve [`IAppFlowProgress`](../../Assets/Packages/com.scaffold.appflow/Runtime/Progress/IAppFlowProgress.cs) / [`IAppFlowErrorHandler`](../../Assets/Packages/com.scaffold.appflow/Runtime/Errors/IAppFlowErrorHandler.cs) from the container).

See [`com.scaffold.appflow` README](../../Assets/Packages/com.scaffold.appflow/README.md) for cross-layer registration patterns, `ILayerPublisher`, `IInitializableLayer`, `ILayerProgressSource`, and `PrepareAsync` on [`IAsyncScopeLayer`](../../Assets/Packages/com.scaffold.appflow/Runtime/Contracts/IAsyncScopeLayer.cs).

Historical two-scope preload notes (superseded by layered scope): [`Startup-Two-Scope-Preload.md`](../../Plans/Startup-Two-Scope-Preload.md).
