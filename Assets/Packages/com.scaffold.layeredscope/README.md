# com.scaffold.layeredscope

UPM package: **stacked VContainer `LifetimeScope` layers** with ordered install, optional `PrepareAsync` against the parent resolver, `IAsyncInitializable` after each child scope is built, async teardown via `IAsyncDisposable`, and optional **cross-layer asset publishing** via **`ILayerPublisher`**.

## Layout

- `Runtime/` — `Scaffold.LayeredScope` assembly (`ApplicationHost`, `ApplicationBootstrap`, contracts).
- `Tests/Editor/` — edit-mode tests (`Scaffold.LayeredScope.Tests`).
- `Samples/` — optional sample scene and layers (`Scaffold.LayeredScope.Samples`, not auto-referenced). Start with **[`Samples/README.md`](Samples/README.md)** for a three-layer **`ILayerPublisher`** walkthrough.

## Dependencies

- [VContainer](https://github.com/hadashiA/VContainer) (`jp.hadashikick.vcontainer`), declared in `package.json`.

## Integration

- Subclass `ApplicationBootstrap` on a root `LifetimeScope`, or construct `ApplicationHost` with an existing root scope.
- Implement `IScopeLayer` (and `IAsyncScopeLayer` when you need `PrepareAsync`).
- Register services as `IAsyncInitializable` when they must run after the layer’s container is created.

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

## Migration

Implementations that today load in `PrepareAsync` and stash instances in private fields for `RegisterInstance` in `Install` can move loading into an `IAsyncInitializable` type that publishes via **`ILayerPublisher`** in a **parent** layer, or keep the existing pattern if same-layer injection is acceptable.
