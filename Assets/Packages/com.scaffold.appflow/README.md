# com.scaffold.appflow

UPM package: **stacked VContainer `LifetimeScope` layers** with ordered install, optional `PrepareAsync` against the parent resolver, `IAsyncInitializable` after each child scope is built, async teardown via `IAsyncDisposable`, optional **cross-layer asset publishing** via **`ILayerPublisher`**, a central **`IAppFlowErrorHandler`** (single `Debug.LogError` sink with exception deduplication), and runtime **`IAppFlowProgress`** for layer queue / status / outcomes (consumable by loading UI in other packages).

## Layout

- `Runtime/` — `Scaffold.AppFlow` assembly (`AppFlowHost`, `AppFlowRoot`, contracts, `Errors/`, `Progress/`).
- `Tests/Editor/` — edit-mode tests (`Scaffold.AppFlow.Tests`).
- `Samples/` — optional sample scene and layers (`Scaffold.AppFlow.Samples`, not auto-referenced). Start with **[`Samples/README.md`](Samples/README.md)** for a three-layer **`ILayerPublisher`** walkthrough.

## Dependencies

- [VContainer](https://github.com/hadashiA/VContainer) (`jp.hadashikick.vcontainer`), declared in `package.json`.

## Integration

- Subclass **`AppFlowRoot`** on a root `LifetimeScope`, or construct **`AppFlowHost`** with an existing root scope.
- Implement `IScopeLayer` (and `IAsyncScopeLayer` when you need `PrepareAsync`).
- Register services as `IAsyncInitializable` when they must run after the layer’s container is created (unless you override init with **`IInitializableLayer`** — see below).
- Resolve **`IAppFlowErrorHandler`** to subscribe to **`OnError`** (replaces the removed `LayerFailed` event), or use **`AppFlowRoot.Errors`** for the same singleton without resolving from the container.
- Resolve **`IAppFlowProgress`** to observe **`Current`** session, **`Changed`**, and **`WhenSessionCompleted()`** (not a ViewModel; bind in your UI layer), or use **`AppFlowRoot.Progress`** for early bootstrap observers (e.g. loading screens).
- Optionally implement **`ILayerProgressSource`** on a layer: expose **`Progress`** (0–1) and raise **`ProgressChanged`**; the host subscribes during that layer’s init wave and maps values to **`SubProgress`**. Layers without this interface still go from **0 → 1** when the layer reaches **`Ready`**.

## Sessions

- **`AppFlowRoot`** wraps startup in **`BeginSession("Startup", layerCount)`** / **`EndSession(fault)`** around **`InstallAllAsync`**.
- **`AppFlowHost.PushAsync`** / **`PopAsync`** outside an active session start a short ad-hoc session (`Push:<Name>` / `Pop:<Name>`) so progress always has a consistent shape.

See the consuming project’s documentation for full bootstrap examples (e.g. Meta / Campaign application hosts).

## Cross-layer registration patterns

### 1. Static parent service, constructor-injected in child

Register types in a parent layer; child scopes inherit VContainer parent registrations. Consumers in a pushed child layer resolve them normally.

### 2. Async-loaded asset published from a parent layer (primary)

Use this when an ancestor must **asynchronously load** an instance and descendants should **constructor-inject** it without `PrepareAsync` field plumbing or per-consumer factories.

- The host registers **`ILayerPublisher`** into each new layer scope (implementation: internal **`LayerPublisher`**). Use **`AssetPublisherBase<TAsset>`** for a standard load-then-publish flow, or plain `IAsyncInitializable` types that call **`Publish` / `PublishMany`** on **`ILayerPublisher`** after loading.
- Published registrations are **replayed into the next child scope’s** `IContainerBuilder` when a descendant layer is pushed. They are **not** resolvable in the **publishing** layer’s own container (the scope is already built before the init wave runs). Put loaders in a **parent** layer and consumers in a **child** layer.
- Loader-specific helpers (Addressables keys, REST clients, etc.) stay **outside** this package — implement them as extension methods or types in the module that owns the loader. This assembly defines **`ILayerPublisher`**, **`AssetPublisherBase<T>`**, and host wiring.

Example (one-line registration in the **publisher** layer):

```csharp
builder.Register<MyAssetProvider>(Lifetime.Singleton).As<IAsyncInitializable>();
```

End-to-end sample code lives under **`Samples/`** (layers + [`Samples/README.md`](Samples/README.md)).

### 3. Same-layer injection (alternative)

When the loaded instance must be constructor-injected in the **same** layer that loads it, use classic VContainer patterns (factory registration, `Register` delegate, etc.) instead of **`ILayerPublisher`**.

## `IAsyncScopeLayer.PrepareAsync`

Reserve `PrepareAsync(IObjectResolver parent, CancellationToken ct)` for **read-only** inspection of the parent resolver (for example deciding which child layers to push). Do **not** use it to register services; use `Install` and/or **`ILayerPublisher`** instead.

## `IInitializableLayer` (init wave override)

After a child scope is built, the host normally collects **`IAsyncInitializable`** registrations and runs them through **`IInLayerScheduler`**. Implement **`IInitializableLayer`** to replace or wrap that behavior:

```csharp
Task InitializeAsync(ILayerInitRunner runner, CancellationToken ct);
```

**`ILayerInitRunner`** exposes **`Scope`**, **`PendingInitializables`**, and **`RunDefaultInitAsync(ct)`** (delegates to the scheduler). Typical patterns:

- **Replace** the default wave: implement `InitializeAsync` without calling **`RunDefaultInitAsync`** (only if you truly do not need registered initializables).
- **Before** the wave: resolve services from **`runner.Scope`**, do custom work, then **`await runner.RunDefaultInitAsync(ct)`**.
- **After** the wave: **`await runner.RunDefaultInitAsync(ct)`** first, then finalize.
- **Parallel** (advanced): **`Task.WhenAll(runner.RunDefaultInitAsync(ct), …)`**.

If you override **`InitializeAsync`**, register one or more **`IAsyncInitializable`** types, and **never** call **`RunDefaultInitAsync`**, those instances are skipped and the host logs a **warning** listing how many were not run.

**Threading:** raise **`ProgressChanged`** and touch Unity APIs on the **main thread** only (do not use **`ConfigureAwait(false)`** on awaits that must resume on Unity’s sync context).

## Bootstrap observation (`AppFlowRoot`)

After **`Configure`** runs, **`AppFlowRoot.Progress`** and **`AppFlowRoot.Errors`** are non-null. A loading-screen **`MonoBehaviour`** can serialize a reference to the root, subscribe to **`Progress.Changed`** in **`Awake`**, and **`await Progress.WhenSessionCompleted()`** to hide the loader.

Ensure the root’s **`LifetimeScope`** runs **`Awake`/`Configure` before** the loading screen: e.g. put **`[DefaultExecutionOrder(-1000)]`** on your **`AppFlowRoot`** subclass (see samples).

## Migration

Implementations that today load in `PrepareAsync` and stash instances in private fields for `RegisterInstance` in `Install` can move loading into an `IAsyncInitializable` type that publishes via **`ILayerPublisher`** in a **parent** layer, or keep the existing pattern if same-layer injection is acceptable.

From **`com.scaffold.layeredscope`**: rename **`ApplicationBootstrap`** → **`AppFlowRoot`**, **`ApplicationHost`** → **`AppFlowHost`**, namespace **`Scaffold.LayeredScope`** → **`Scaffold.AppFlow`**, package id **`com.scaffold.appflow`**.
