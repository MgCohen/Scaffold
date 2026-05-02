# Audit — `com.scaffold.sceneflow`

Senior architect review. Tone: opinionated.

## 1. Summary & verdict

`com.scaffold.sceneflow` is a thin Addressables additive scene loader with a bootstrap-shell toggle (camera/audio/event-system off when additive content owns the world) and a deterministic test seam (`IAddressablesSceneOperations`). It exposes one service interface, `ISceneFlowService`, with `LoadAdditiveAsync` / `UnloadAsync`. There is no `LoadSingleAsync`, no scene transition graph, no scene preload, no "swap" semantics, no concurrent-load arbitration beyond a single shell-counter, and no API for typed scene keys.

Strengths: the `IAddressablesSceneOperations` seam is correct; the shell counter (`shellManagedLoadCount`) handles concurrent additive loads; `UnloadAsync` validates the load id and throws on double-unload; cancellation is respected after `await handle.Task`. The package does what it advertises. It does not pretend to be a flow.

Weakness against the rubric: scenes are keyed by `AssetReference` (strongly typed at the *Addressables* level — a serialized GUID + sub-asset string) but **not** at the C# domain level. Callers pass raw `AssetReference` objects around. There is no `SceneKey<TScene>` style typed handle. A callsite passing the *wrong* `AssetReference` (a prefab instead of a scene asset, the level-2 ref instead of level-1) compiles and only fails at load time. Given the rubric ("Prefer generics and C# typing for compile-time safety"), this is the main miss.

The other miss: `SceneFlowInstaller` accepts `null` for `ISceneFlowBootstrapShell` and silently does nothing. Default values that hide errors are exactly what the rubric forbids.

`Tests/` ships only an asmdef, no tests.

**Verdict: keep, with refactor.** The mechanics are sound; tighten typing, fix the silent null shell, write the missing tests.

---

## 2. Structure

```
com.scaffold.sceneflow/
  Runtime/                                 (Scaffold.SceneFlow.asmdef, autoReferenced, refs:
                                            Unity.Addressables, Unity.ResourceManager,
                                            UnityEngine.UI, VContainer, VContainer.Unity)
    AddressablesSceneOperations.cs         (real impl of test seam)
    LoadingView.cs                         (MonoBehaviour, builds default UI in Awake)
    SceneFlowBootstrapShell.cs             (MonoBehaviour, toggles camera/listener/eventsystem)
    SceneFlowInstaller.cs                  (IInstaller for VContainer)
    SceneFlowService.cs                    (the only flow logic)
    SceneFlowLoadOptions.cs                (readonly struct: ManageBootstrapShell)
    SceneFlowLoadRecord.cs                 (internal: handle + flag)
    SceneFlowLoadResult.cs                 (readonly struct: Guid, name, flag)
    Contracts/
      IAddressablesSceneOperations.cs
      ISceneFlowBootstrapShell.cs
      ISceneFlowService.cs
  Tests/
    Scaffold.SceneFlow.Tests.asmdef        (asmdef only, NO TEST SOURCES)
  README.md, package.json
```

`package.json` declares `com.unity.addressables` 2.9.1. No `VContainer` declaration — the asmdef references it but the package manifest doesn't, which is a real bug at install time on a clean project (see §4.6).

---

## 3. What's good

- **Typed seam for Addressables.** `IAddressablesSceneOperations` (`Contracts/IAddressablesSceneOperations.cs`) lets tests fake `LoadSceneAsync` / `UnloadSceneAsync` deterministically without touching the runtime. README explicitly recommends this for tests.
- **Opaque load token.** `SceneFlowLoadResult` returns a `Guid` to the caller; the caller has no access to the underlying `AsyncOperationHandle<SceneInstance>`. Callers can't cheat and `Addressables.Release` directly. Good encapsulation.
- **Concurrent-load arbitration.** `shellManagedLoadCount` (`SceneFlowService.cs:27`, `:67`, `:98`, `:141`) keeps the shell active until the *last* additive load with `ManageBootstrapShell=true` unloads. Right behavior.
- **Rollback on failed load.** `RollbackShellReservationIfNeeded` + `ReleaseHandleIfNeeded` (`SceneFlowService.cs:91-103`, `:149-155`) decrement the counter and release the handle if the load throws after reserving the shell.
- **Cancellation honored.** `await handle.Task; cancellationToken.ThrowIfCancellationRequested();` (`SceneFlowService.cs:76-77`, `:124-125`). Not perfect (Addressables' `LoadSceneAsync` itself isn't cancellable), but the post-await throw is the right pattern.
- **Double-unload rejected.** `UnloadAsync` throws `InvalidOperationException` on unknown id (`SceneFlowService.cs:107-110`). Fail-fast — the architect will approve.
- **Service is pure C#.** `SceneFlowService` has no `MonoBehaviour` inheritance and no `UnityEngine` API beyond `Addressables.Release` (which is a static gateway, fine). Boundary respected.

---

## 4. Issues & smells

### 4.1 Scenes are stringly-typed at the domain level

Callers pass `AssetReference` directly:

```csharp
SceneFlowLoadResult loaded = await sceneFlowService.LoadAdditiveAsync(
    levelSceneReference,                  // <-- any AssetReference, no scene-typing
    SceneFlowLoadOptions.Default,
    ct);
```

`AssetReference` is the Unity Addressables type — a serialized GUID and sub-object string. Two callsites loading "MainMenu" and "Battle" both hold `AssetReference` fields somewhere; nothing prevents passing the menu reference to the battle loader. The rubric explicitly favors compile-time typing.

A `SceneKey<TScene>` (or `record SceneKey(string Address)` enforced via Roslyn analyzer + source generator from a settings asset) would let the architect write `LoadAdditiveAsync(SceneKeys.Battle)` and have the compiler reject `LoadAdditiveAsync(SceneKeys.MainMenu)` if the consumer is typed `BattleSceneLoader`. The Navigation package already has a config-driven `ViewConfig` registry — the same pattern fits scenes.

### 4.2 `SceneFlowInstaller` silently no-ops with `null` shell

`SceneFlowInstaller.cs:9-21`:

```csharp
public SceneFlowInstaller(ISceneFlowBootstrapShell bootstrapShell = null)
{
    this.bootstrapShell = bootstrapShell;
}

public void Install(IContainerBuilder builder)
{
    if (bootstrapShell != null)
    {
        builder.RegisterInstance(bootstrapShell).As<ISceneFlowBootstrapShell>();
    }
    // ...
    ISceneFlowBootstrapShell shellForService = bootstrapShell;
    builder.Register<ISceneFlowService>(c =>
    {
        IAddressablesSceneOperations ops = c.Resolve<IAddressablesSceneOperations>();
        return new SceneFlowService(ops, shellForService);   // <-- null is happy here
    }, Lifetime.Singleton);
}
```

The README says: *"`SceneFlowInstaller` accepts null shell; null-object `ISceneFlowBootstrapShell` is applied when omitted."* That is **a lie** — there is no null-object registered, the field is just null and `SceneFlowService` does `bootstrapShell?.SetAdditiveContentActive(...)`. So when an integration assumes `Resolve<ISceneFlowBootstrapShell>()` will work, it will throw. And when `manageShell=true` but no shell, the load proceeds with no visual handoff (camera double-rendering a black frame is a real shipped bug). Either:

- Require `ISceneFlowBootstrapShell` (rubric-aligned: fail-fast); the caller passes a `NullSceneFlowBootstrapShell` if they truly don't want it.
- Or actually register a `NullSceneFlowBootstrapShell` and remove the `?.` from the service.

Right now you have the worst of both worlds: a default value that hides a misconfiguration, and a comment claiming the opposite.

### 4.3 `Tests/` directory has no tests

`Tests/Scaffold.SceneFlow.Tests.asmdef` is the only file. The README claims "Tests use fakes", "Cover: double-unload rejection, shell toggles, exception propagation on failed load." None of that is in the repo. This is documentation drift that will bite.

Required tests (the README's own list is correct):
- Double-unload throws.
- `ManageBootstrapShell=true` toggles shell on first load, off on last unload, stays on through interleaved loads.
- Failed `LoadSceneAsync` releases the handle and rolls back the shell reservation.
- Cancellation between `await handle.Task` and `ThrowIfCancellationRequested` (use a fake `IAddressablesSceneOperations`).

### 4.4 `LoadingView` builds Unity UI imperatively in `Awake`

`LoadingView.cs:16-24, :56-130` — if `presenterRoot` is null, it builds a Canvas + ScreenSpaceOverlay + GraphicRaycaster + dimmer + progress bar in code. This is presentation logic in a runtime asmdef that already mixes UI concerns. Two problems:

- It violates the rubric's "Keep Unity and pure C# at separate boundaries" — but more importantly, it puts UI construction in a *scene flow* package. Loading UI is a navigation/presentation concern, not a scene-flow concern. The README itself says "Does **not** own transition loading UI; callers that load scenes also own `Show`/`Hide`" — and yet the package ships `LoadingView`.
- The "build a UI in code if nothing is wired up" branch is convenience that hides misconfiguration. If `presenterRoot` is null, that's a user error; throw or `Debug.LogError` and bail.

Move `LoadingView` to its own package (or to `com.scaffold.navigation`) or strip the auto-build. AppFlow's `SampleLoadingScreen` already references it cross-package — that linkage is fine, but the *home* should not be SceneFlow.

### 4.5 `LoadingView.IsVisible` is serialized

`LoadingView.cs:8-10`:

```csharp
public bool IsVisible => isVisible;
[SerializeField] private bool isVisible;
```

A view's runtime visibility state should not be a serialized inspector field. It's overwritten on `Awake().Hide()` anyway, so the serialized value is meaningless. Remove `[SerializeField]`.

### 4.6 `package.json` is missing the VContainer dependency

`package.json:12-14`:

```json
"dependencies": {
    "com.unity.addressables": "2.9.1"
}
```

But `Scaffold.SceneFlow.asmdef` references `VContainer` and `VContainer.Unity`. On a fresh install pulling this UPM package alone, the asmdef won't compile. Either remove the VContainer reference and ship `SceneFlowInstaller` in a separate `Scaffold.SceneFlow.Container` asmdef (the Navigation package does exactly this — a good pattern), or declare the dep in `package.json`.

The Navigation package's split (`Scaffold.Navigation` runtime + `Scaffold.Navigation.Container` for VContainer wiring) is the right precedent.

### 4.7 `LoadSceneAsync` parameters defaulted

`AddressablesSceneOperations.cs:11`:

```csharp
public AsyncOperationHandle<SceneInstance> LoadSceneAsync(
    AssetReference sceneReference,
    LoadSceneMode loadSceneMode,
    bool activateOnLoad = true,
    int priority = 100)
```

`activateOnLoad=true` and `priority=100` are reasonable defaults *for a leaf method calling Addressables*. But `SceneFlowService.RunAdditiveLoadAsync` calls `LoadSceneAsync(sceneReference, LoadSceneMode.Additive, true, 100)` (`SceneFlowService.cs:47`) hard-coded. There is no way to defer activation (which is the entire reason `activateOnLoad=false` exists for split-second hand-offs in pre-loaded scenes). Either:

- Surface `activateOnLoad` and `priority` through `SceneFlowLoadOptions`.
- Drop the parameters from `IAddressablesSceneOperations` (only one caller, hard-coded).

Defaulting these in the contract is over-abstraction (`IInLayerScheduler`-style) — the only known consumer doesn't exercise the flexibility.

### 4.8 `SceneFlowLoadOptions.Default` static factory

`SceneFlowLoadOptions.cs:5`: `public static SceneFlowLoadOptions Default => new SceneFlowLoadOptions(true);`. This is fine but the value `true` for `ManageBootstrapShell` is the opinionated default. Document it. Better: make `SceneFlowLoadOptions` non-`Default`-able and force callers to declare intent at every call site.

### 4.9 `SceneFlowLoadResult.SceneName ?? string.Empty`

`SceneFlowLoadResult.cs:11`. Defaulting a null name to empty string is a minor "default that hides errors" smell. The name comes from `handle.Result.Scene.name` (`SceneFlowService.cs:85`), which is non-null after a successful load. The defensive default is not earning its keep.

### 4.10 `RemoveActiveLoadRecord` decrements `shellManagedLoadCount` based on `record.ManageBootstrapShell`, not on the *current* state

`SceneFlowService.cs:135-147`. If `LoadAdditiveAsync` reserved the shell (`shellManagedLoadCount++`) and committed, `record.ManageBootstrapShell = true`. Unload symmetrically decrements. All good. But if a future path mutates `manageShell` between load and unload (e.g. an "abandon and re-enter shell" feature), the symmetry breaks. Annotate the invariant or move the bookkeeping into a dedicated "shell reservation" type so that's the only place it lives.

### 4.11 `SceneFlowService.activeLoads` is not concurrency-safe

`SceneFlowService.cs:25` is a plain `Dictionary<Guid, SceneFlowLoadRecord>`. Two concurrent `LoadAdditiveAsync` calls will both call `Addressables.LoadSceneAsync`, both `await`, then race to add to the dictionary. `Dictionary` is *not* thread-safe; a concurrent add can corrupt internal buckets. In Unity practice everything resumes on the main thread after `await handle.Task`, so the race is theoretical — but make the contract explicit: document "main-thread only", or use a concurrent collection.

### 4.12 `LoadingView` and `SceneFlowBootstrapShell` are `MonoBehaviour` in a runtime asmdef

Both are presentation/composition Unity types. They belong in a package consumer's scene, not in the runtime library. Keeping them here means anyone referencing `Scaffold.SceneFlow` (a pure-C# service) drags Unity UI into the assembly. Today the asmdef already references `UnityEngine.UI` so the cost is paid; the design is already crossed. Splitting `Scaffold.SceneFlow.Runtime` (pure) from `Scaffold.SceneFlow.Components` (Mono) would honor the rubric.

---

## 5. Suggested before/after

### 5.1 Typed scene keys

**Before:**
```csharp
SceneFlowLoadResult loaded = await sceneFlowService.LoadAdditiveAsync(
    levelSceneReference,                  // raw AssetReference
    SceneFlowLoadOptions.Default,
    ct);
```

**After:**
```csharp
public readonly struct SceneKey<TScene> where TScene : ISceneTag
{
    public SceneKey(AssetReference reference) { Reference = reference; }
    public AssetReference Reference { get; }
}

public interface ISceneTag { }
public sealed class BattleScene  : ISceneTag { }
public sealed class MenuScene    : ISceneTag { }

public interface ISceneFlowService
{
    Task<SceneFlowLoadResult<TScene>> LoadAdditiveAsync<TScene>(
        SceneKey<TScene> key,
        SceneFlowLoadOptions options,
        CancellationToken ct = default)
        where TScene : ISceneTag;
}
```

Then a generated `SceneKeys` static class (source-generator-driven from your `NavigationSettings`-style settings asset) exposes `SceneKeys.Battle`, `SceneKeys.MainMenu` typed by the marker. Compile-time check that a "battle loader" can't load the menu scene. Pair with an analyzer that bans raw `AssetReference` parameters in this package.

### 5.2 Require `ISceneFlowBootstrapShell` (or a real null-object)

**Before** (`SceneFlowInstaller.cs:9-12`):
```csharp
public SceneFlowInstaller(ISceneFlowBootstrapShell bootstrapShell = null) { ... }
```

**After** — fail-fast:
```csharp
public SceneFlowInstaller(ISceneFlowBootstrapShell bootstrapShell)
{
    this.bootstrapShell = bootstrapShell ?? throw new ArgumentNullException(nameof(bootstrapShell));
}

// And expose:
public sealed class NullSceneFlowBootstrapShell : ISceneFlowBootstrapShell
{
    public void SetAdditiveContentActive(bool active) { }
}
```

Caller writes `new SceneFlowInstaller(new NullSceneFlowBootstrapShell())` if they really mean "no shell". No silent no-op.

### 5.3 Drop `Tests/` skeleton or fill it

Option A: delete the empty asmdef.
Option B: implement the four tests the README claims:

```csharp
[Test]
public async Task DoubleUnload_Throws()
{
    var ops = new FakeOps();
    var svc = new SceneFlowService(ops, new NullSceneFlowBootstrapShell());
    var result = await svc.LoadAdditiveAsync(ops.SomeRef(), SceneFlowLoadOptions.Default);
    await svc.UnloadAsync(result);
    Assert.ThrowsAsync<InvalidOperationException>(() => svc.UnloadAsync(result));
}
```

Etc.

### 5.4 Remove `_priority`/`_activateOnLoad` from contract or surface in options

**Before:**
```csharp
public interface IAddressablesSceneOperations
{
    AsyncOperationHandle<SceneInstance> LoadSceneAsync(
        AssetReference sceneReference, LoadSceneMode loadSceneMode,
        bool activateOnLoad = true, int priority = 100);
}
```

**After:**
```csharp
public interface IAddressablesSceneOperations
{
    AsyncOperationHandle<SceneInstance> LoadSceneAsync(AssetReference sceneReference, LoadSceneMode mode);
}
```

And add `ActivateOnLoad`, `Priority` to `SceneFlowLoadOptions` only if a real consumer needs them. Pay-for-what-you-use.

### 5.5 Move `LoadingView` out

Move `LoadingView` to `com.scaffold.navigation` (or a dedicated `com.scaffold.loadingui`). SceneFlow becomes a pure C# service + `ISceneFlowBootstrapShell` contract + `IAddressablesSceneOperations` contract. The two `MonoBehaviour`s (`SceneFlowBootstrapShell`, `LoadingView`) belong in a `Scaffold.SceneFlow.Components` asmdef (or a consumer's project), not in the core service.

---

## 6. Easy wins (5–8)

1. **Add the four tests the README claims** in `Tests/`.
2. **Throw on null `ISceneFlowBootstrapShell`** in `SceneFlowInstaller` (`SceneFlowInstaller.cs:9-12`) or actually register a null-object — pick one and align README text.
3. **Add VContainer to `package.json`** (`package.json:12-14`).
4. **Drop `[SerializeField]` from `LoadingView.isVisible`** (`LoadingView.cs:10`).
5. **Strip the auto-built default UI from `LoadingView.Awake`** (`LoadingView.cs:18-24, 56-130`) — log an error and bail if `presenterRoot` is null.
6. **Remove `activateOnLoad` and `priority` parameters** from `IAddressablesSceneOperations` (or surface them in options).
7. **Inline `RunAdditiveLoadAsync` and `CompleteAdditiveLoadAsync`** in `SceneFlowService` — the helper-per-await style is the same over-decomposition smell as in AppFlow.
8. **Document main-thread invariant** in `SceneFlowService` or use a thread-safe collection for `activeLoads`.

---

## 7. Bigger refactors

### 7.1 Typed scene registry + analyzer

Pair a `SceneRegistry` ScriptableObject (mirroring `NavigationSettings`) with a Roslyn source generator that emits `SceneKeys.Battle`, `SceneKeys.MainMenu` typed by marker classes. Analyzer rejects raw `AssetReference` use in any consumer of `Scaffold.SceneFlow`.

### 7.2 Split asmdefs along the rubric boundary

```
Scaffold.SceneFlow                       (pure C#: ISceneFlowService, contracts, SceneFlowService)
Scaffold.SceneFlow.Addressables          (AddressablesSceneOperations — UnityEngine.AddressableAssets)
Scaffold.SceneFlow.Components            (MonoBehaviours: SceneFlowBootstrapShell, LoadingView)
Scaffold.SceneFlow.Container             (SceneFlowInstaller — VContainer)
```

This is the same split Navigation already uses (`Container/`). Apply it consistently.

### 7.3 Loading lifecycle hook

Right now the README's example wraps `LoadAdditiveAsync` in `try/finally` with `loadingView.Show()/.Hide()`. That is presentation + flow control mixed at the call site. Introduce a `ISceneLoadingPresenter` (or pass a delegate to `LoadAdditiveAsync(SceneKey, IProgress<float>, CancellationToken)`) so consumers don't repeat the show/hide dance. Bonus: route the Addressables `handle.PercentComplete` into the progress reporter, which is currently dropped.

---

## 8. Organization & docs

### Asmdef hygiene
- `Scaffold.SceneFlow.asmdef` references `VContainer` + `VContainer.Unity`, but `package.json` does not declare VContainer. Bug.
- Tests asmdef is empty. Delete or populate.
- `noEngineReferences=false` is correct; this package legitimately needs Unity.

### Documentation
- README is detailed and well-organized. Three issues:
  - It claims `SceneFlowInstaller` registers a "null-object `ISceneFlowBootstrapShell`" — false (§4.2).
  - It claims tests cover double-unload, shell toggles, exception propagation — none exist.
  - "Does **not** own transition loading UI" but the package ships `LoadingView`. Contradiction.
- Public APIs lack XML docs.

### Naming & layout
- `SceneFlowLoadRecord` is `internal` — correct (it holds the raw `AsyncOperationHandle`).
- `SceneFlowLoadResult` is `readonly struct` with a `Guid` — good opaque token.
- `Contracts/` holds three interfaces. Fine.

### Sources / patterns to reference
- Cysharp UniTask scene loading wrappers (`UniTaskAddressables.LoadSceneAsync`) — zero alloc.
- Unity Addressables docs on `Activate()` for split-second cutovers — relevant if you keep the parameter.
- MAUI Shell route concept + Unity SceneAsset typed keys — the "typed scene key" pattern.
