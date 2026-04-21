# AppFlow samples

Sample assembly: **`Scaffold.AppFlow.Samples`** (not auto-referenced by other packages; enable in **Package Manager** for local samples). The assembly references **`Scaffold.SceneFlow`** so the loading demo can reuse **`LoadingView`** (full-screen Canvas + bar) without duplicating UI code in the sample.

## Layers

### `SampleAssetsLayer`

Derives from **`AssetPublisherBase<SampleAsset>`**: abstract **`LoadAssetAsync`**, optional override **`Publish`**. Loads a placeholder `SampleAsset` and publishes it so child layers can inject it. Constructor-inject **`ILayerPublisher`**.

### `SampleConfigsLayer`

Implements **`IInitializableLayer`** and **`ILayerProgressSource`**: the layer runs **`WarmupAsync`** on **`SampleConfigService`** via **`ILayerInitRunner.Scope`**, then **`RunDefaultInitAsync`**, and reports **sub-progress** (0–1) through **`Progress` / `ProgressChanged`** (no `RegisterInstance(this)` on the layer).

### `SampleFeatureLayer`

Consumes the published `SampleAsset` and `ISampleConfigService` via constructor injection.

## Bootstrap

**`SampleAppFlowRoot`**: `[DefaultExecutionOrder(-1000)]` so **`Configure`** runs before other **`MonoBehaviour`** scripts; initial push installs **assets** then **config**; **`OnReadyAsync`** pushes **feature** then pops it. No separate “feature assets” layer.

**`SampleLoadingScreen`**: optional demo on the **`LoadingScreen`** GameObject — references **`AppFlowRoot`** and **`Scaffold.SceneFlow.LoadingView`**, shows the view on **`Awake`**, updates the bar with a **weighted** normalized value `(CompletedLayers + Current.SubProgress) / TotalLayers` on **`IAppFlowProgress.Changed`**, then **`await Progress.WhenSessionCompleted()`**, sets the bar to **1** and hides the view. Wire **`AppFlowRoot`** to the scene’s **`SampleAppFlowRoot`** and **`LoadingView`** to the sibling component.

## Scene

**`Scenes/AppFlowSample.unity`** — root **`LifetimeScope`** uses **`SampleAppFlowRoot`**; **`LoadingScreen`** has **`LoadingView`** + **`SampleLoadingScreen`** with references assigned.
