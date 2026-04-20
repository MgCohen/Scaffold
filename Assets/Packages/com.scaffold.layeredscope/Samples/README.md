# LayeredScope samples

Three layers, three `Install` methods:

| Layer | Role |
|-------|------|
| **`SampleAssetsLayer`** | Gateway warmup + **`AssetPublisherBase<T>`** subclasses that load and **`ILayerPublisher.Publish`** shared + feature payloads for **descendant** scopes only. |
| **`SampleConfigsLayer`** | Config service (`ISampleConfigService`). |
| **`SampleFeatureLayer`** | **`SampleFeatureService`** constructor-injects **`SampleAsset`**, **`SharedSampleAsset`**, and config — published types come from the asset layer replay, not from this `Install`. |

## `AssetPublisherBase<TAsset>`

In **`Scaffold.LayeredScope`**: abstract **`LoadAssetAsync`**, optional override **`Publish`** for `Publish<TInterface,TImpl>`. Subclass per asset type or per loader (Addressables, Resources, HTTP, etc.). Constructor-inject **`ILayerPublisher`**.

## `ILayerPublisher` rules (host)

1. **`Publish`** runs during **`IAsyncInitializable`** after the layer scope is built → **not** resolvable in the **publishing** layer.
2. The **next** pushed child replays ancestor deltas → consumers **ctor-inject** published types.
3. Host implementation is **`LayerPublisher`** (internal); you only depend on **`ILayerPublisher`**.

## Bootstrap

**`SampleApplicationBootstrap`**: initial push installs **assets** then **config**; **`OnReadyAsync`** pushes **feature** then pops it. No separate “feature assets” layer.

## Scene

**`Scenes/LayeredScopeSample.unity`** — root **`LifetimeScope`** uses **`SampleApplicationBootstrap`**.

Package API overview: [`README.md`](../README.md).
