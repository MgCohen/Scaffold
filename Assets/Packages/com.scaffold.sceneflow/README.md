# com.scaffold.sceneflow

# Scene Flow (Additive Addressables)

## TL;DR

- Purpose: load and unload **Addressable scenes** in **Additive** mode while the **Bootstrap** scene stays loaded.
- Location: `Assets/Packages/com.scaffold.sceneflow/Runtime/` (`Scaffold.SceneFlow` assembly).
- Depends on: Unity Addressables, ResourceManager, VContainer (`SceneFlowInstaller` implements `IInstaller`).
- Used by: any composition layer that registers `SceneFlowInstaller` (not wired from default Bootstrap infra).
- Runtime/Editor: runtime; EditMode tests in `Scaffold.SceneFlow.Tests`.
- Keywords: additive scene, Addressables, Bootstrap shell.

## Responsibilities

- Owns `ISceneFlowService` for additive load/unload with tracked `SceneFlowLoadResult` tokens.
- Owns `IAddressablesSceneOperations` as the single seam for `Addressables.LoadSceneAsync` / `Addressables.UnloadSceneAsync` (tests use fakes).
- Owns optional `ISceneFlowBootstrapShell` (e.g. `SceneFlowBootstrapShell` MonoBehaviour in the shell scene) to disable Bootstrap camera/listener while additive content owns the world.
- Does **not** own transition loading UI; callers that load scenes also own `Show`/`Hide` for loading presentation (see `LoadingView` in `Scaffold.App.Bootstrap`).
- Does **not** own `INavigation`, view models, or domain gameplay logic.
- Does **not** replace `IAddressablesGateway` for **asset** loads (prefabs, etc.); scene loads use the Addressables **scene** API explicitly.

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure / edge behavior |
| --- | --- | --- | --- | --- |
| `ISceneFlowService` | Additive load/unload | `AssetReference`, options, cancellation | `SceneFlowLoadResult` / task | Throws if reference null; unload throws if id unknown or already unloaded |
| `SceneFlowLoadOptions` | Load behavior flags | `ManageBootstrapShell` | n/a | Default: shell managed |
| `SceneFlowLoadResult` | Opaque load token | from successful load | `LoadId`, `SceneName`, `ManageBootstrapShell` | Must be passed to `UnloadAsync` |
| `ISceneFlowBootstrapShell` | Shell visibility | `SetAdditiveContentActive` | n/a | Disables Bootstrap camera/listener when additive content is active (implementation-specific) |
| `SceneFlowInstaller` | VContainer `IInstaller` | optional `ISceneFlowBootstrapShell` | n/a | Registers null-object shell when reference is null |
| `IAddressablesSceneOperations` | Test/production seam | scene reference / handles | `AsyncOperationHandle<SceneInstance>` | Real impl delegates to `Addressables` |

## Setup / Integration

1. Reference `Scaffold.SceneFlow` from the assembly that should register scene flow (e.g. a dedicated installer layer or future bootstrap extension).
2. From that layer’s `Install(IContainerBuilder)` implementation, call `Install(builder, new SceneFlowInstaller(sceneFlowBootstrapShell))` using `LayerInstallerBase.Install`, or `new SceneFlowInstaller(shell).Install(builder)` directly.
3. Optionally place `SceneFlowBootstrapShell` in the Bootstrap scene and pass its component instance into `SceneFlowInstaller` so `ManageBootstrapShell` loads toggle camera/listener.
4. Resolve `ISceneFlowService` from the container after the layer that registers it has been built.

Common mistakes: registering `SceneFlowInstaller` in multiple layers; releasing Addressables scene handles manually outside `ISceneFlowService`.

## How to Use

1. Inject `ISceneFlowService` into the coordinator for your flow (not from core domain assemblies).
2. Optionally show a loading UI (`LoadingView` or any view) before/around `LoadAdditiveAsync` / `UnloadAsync` in the same coordinator.
3. Call `LoadAdditiveAsync` with a valid **scene** `AssetReference` and options.
4. Keep the returned `SceneFlowLoadResult` until the content session ends.
5. Call `UnloadAsync` with that result.
6. Keep all screen UI under Navigation in the Bootstrap scene; 3D lives in the additive scene.

## Examples

### Minimal load/unload

```csharp
loadingView.Show();
try
{
    SceneFlowLoadResult loaded = await sceneFlowService.LoadAdditiveAsync(
        levelSceneReference,
        SceneFlowLoadOptions.Default,
        cancellationToken);

    try
    {
        // Gameplay using the additive scene.
    }
    finally
    {
        await sceneFlowService.UnloadAsync(loaded, cancellationToken);
    }
}
finally
{
    loadingView.Hide();
}
```

### Pseudo product flow (not shipped as one block)

Main menu (Navigation) → user picks a level → coordinator shows loading → `LoadAdditiveAsync` → gameplay in additive scene → loading → `UnloadAsync` → return UI to main menu via Navigation.

## Best Practices

- Treat `SceneFlowLoadResult` as **opaque**; do not persist it across app restarts.
- Use `ManageBootstrapShell: true` for full-screen gameplay scenes that bring their own camera; use `false` for lightweight additive tools if Bootstrap must keep drawing.
- Prefer **one** owner for spawner prefab loads vs scene dependency loads; see discussion on Addressables reference counting in team docs.
- Run `.agents/scripts/validate-changes.cmd` after changes to this module.

## Anti-Patterns

- Calling `Addressables.LoadSceneAsync` for routed scenes **outside** `ISceneFlowService`, duplicating handle lifecycle.
- Unloading the Bootstrap scene or deactivating the whole shell to “reset” gameplay.
- Putting `MonoBehaviour` gameplay logic in `Scaffold.SceneFlow` (keep Unity presentation at app/bootstrap boundaries; see [Architecture.md](../../../Architecture.md)).

## Testing

- Assembly: `Scaffold.SceneFlow.Tests` (EditMode).
- Fakes implement `IAddressablesSceneOperations` with `ResourceManager.CreateCompletedOperation` for deterministic tests.
- Cover: double-unload rejection, shell toggles, exception propagation on failed load.

## AI Agent Context

- Extend `IAddressablesSceneOperations` if you need PlayMode integration tests with real Addressables.
- `SceneFlowInstaller` accepts null shell; null-object `ISceneFlowBootstrapShell` is applied when omitted.
- Product level loads use `ISceneFlowService` additively from `GameSessionCoordinator`; battle setup lives in `BattleGameFactory`.

## Related

- `../com.scaffold.addressables/README.md` — asset gateway (not scene loads).
- `../com.scaffold.navigation/README.md` — UI stack ownership.
- `../com.scaffold.bootstrap/README.md` — `LoadingView` for caller-owned transition UI.
- `Plans/SceneFlow/SceneFlow-ExecPlan.md` — delivery plan and progress.

## Changelog

- Decoupled SceneFlow from default Bootstrap wiring; removed loading presenter from `ISceneFlowService`; `SceneFlowInstaller` implements `IInstaller`; `UnloadAsync` no longer takes unload options struct.
- Initial `Scaffold.SceneFlow` module with `ISceneFlowService`, Bootstrap shell hook, tests.
