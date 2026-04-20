# Application startup (composition root)

There is no `com.scaffold.bootstrap` package in this repository.

Wire your game by **subclassing** [`ApplicationBootstrap`](../../Assets/Packages/com.scaffold.layeredscope/Runtime/ApplicationBootstrap.cs) on a root `LifetimeScope` in **your** assembly:

- Override `ConfigureApplication(IContainerBuilder)` to register root services (for example navigation holder, shared contracts).
- Implement `GetInitialLayers()` to return an ordered sequence of [`IScopeLayer`](../../Assets/Packages/com.scaffold.layeredscope/Runtime/Contracts/IScopeLayer.cs) instances. The host pushes each layer as a child `LifetimeScope`, runs [`IAsyncInitializable`](../../Assets/Packages/com.scaffold.layeredscope/Runtime/Contracts/IAsyncInitializable.cs) after each push, and supports async teardown via `IAsyncDisposable` on pop.
- Optionally override `CreateScheduler()` to supply an [`IInLayerScheduler`](../../Assets/Packages/com.scaffold.layeredscope/Runtime/Contracts/IInLayerScheduler.cs) for init-wave ordering.
- Resolve [`ILayerResolver`](../../Assets/Packages/com.scaffold.layeredscope/Runtime/Contracts/ILayerResolver.cs) from the container when you need the **current top** `IObjectResolver` (for example navigation injecting view models into the active layer).

See [`com.scaffold.layeredscope` README](../../Assets/Packages/com.scaffold.layeredscope/README.md) for cross-layer registration patterns, `ILayerPublisher`, and `PrepareAsync` on [`IAsyncScopeLayer`](../../Assets/Packages/com.scaffold.layeredscope/Runtime/Contracts/IAsyncScopeLayer.cs).

Historical two-scope preload notes (superseded by layered scope): [`Startup-Two-Scope-Preload.md`](../../Plans/Startup-Two-Scope-Preload.md).
