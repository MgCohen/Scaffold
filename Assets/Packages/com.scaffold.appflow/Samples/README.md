# AppFlow samples

Sample assembly: **`Scaffold.AppFlow.Samples`** (not auto-referenced by other packages; enable in **Package Manager** for local samples).

## Layers

### `SampleAssetsLayer`

Derives from **`AssetPublisherBase<SampleAsset>`**: abstract **`LoadAssetAsync`**, optional override **`Publish`**. Loads a placeholder `SampleAsset` and publishes it so child layers can inject it. Constructor-inject **`ILayerPublisher`**.

### `SampleConfigsLayer`

Implements **`IInitializableLayer`** and **`ILayerProgressSource`**: the layer runs **`WarmupAsync`** on **`SampleConfigService`** via **`ILayerInitRunner.Scope`**, then **`RunDefaultInitAsync`**, and reports **sub-progress** (0–1) through **`Progress` / `ProgressChanged`** (no `RegisterInstance(this)` on the layer).

### `SampleFeatureLayer`

Consumes the published `SampleAsset` and `ISampleConfigService` via constructor injection.

## Bootstrap

**`SampleAppFlowRoot`**: `[DefaultExecutionOrder(-1000)]` so **`Configure`** runs before other **`MonoBehaviour`** scripts; initial push installs **assets** then **config**; **`OnReadyAsync`** pushes **feature** then pops it. No separate “feature assets” layer.

**`SampleLoadingScreen`**: optional demo — assigns **`AppFlowRoot`**, logs **`Progress.Changed`**, and awaits **`Progress.WhenSessionCompleted()`**. Add it to the sample scene with **`AppFlowRoot`** wired in the inspector.

## Scene

**`Scenes/AppFlowSample.unity`** — root **`LifetimeScope`** uses **`SampleAppFlowRoot`**.
