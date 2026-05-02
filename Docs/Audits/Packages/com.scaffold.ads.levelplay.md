# com.scaffold.ads.levelplay — Audit

## Summary
The IronSource LevelPlay implementation of `Scaffold.Ads`. Three services (`LevelPlayRewardedAdService`, `LevelPlayInterstitialAdService`, `LevelPlayBannerAdService`) wrap the corresponding `Unity.Services.LevelPlay` ad objects, are constructed by `LevelPlayAdProvider` after the SDK init callback, and are wired to the abstraction via `LevelPlayAdConfigurationSO.CreateProvider()`. The provider-side factory pattern is correctly followed; LevelPlay types do not leak into `com.scaffold.ads`.

The implementation does enough work to demo and ship, but it has provider-shape bugs that are visible to the abstraction and that violate the rubric: `Task.Run` reload off the main thread (LevelPlay requires main-thread calls), single-field `currentShowingPlacement` racing across impressions, banner "registered but never loaded", cross-talk between identical ad-unit-IDs, and an editor-config fallback on production platforms. Test scripts live in the runtime asmdef next to production code, and `UnityAdsTester.cs` is a hidden bootstrap that auto-initializes the entire ad stack from a `MonoBehaviour.Start()` — which fights VContainer.

**Verdict: refactor.** Provider boundary is correct, but the bridging code needs both correctness and lifecycle fixes before this is a calm production path.

## Structure
```
com.scaffold.ads.levelplay/
  Container/
    LevelPlayInstaller.cs                       (registers SO + delegates to AdsInstaller)
    Scaffold.Ads.Levelplay.Container.asmdef
  Runtime/
    Core/
      LevelPlayAdConfigurationSO.cs             (concrete AdConfigurationSO)
      LevelPlayAdProvider.cs                    (IAdProvider; SDK init + service construction)
      LevelPlayPlatformConfig.cs                ([Serializable] struct with placements)
    Banner/        LevelPlayBannerAdService.cs
    Interstitial/  LevelPlayInterstitialAdService.cs
    Rewarded/      LevelPlayRewardedAdService.cs
    Test/
      BannerAdTester.cs                         (MonoBehaviour, runtime asmdef)
      InterstitialAdTester.cs                   (MonoBehaviour, runtime asmdef)
      RewardedAdTester.cs                       (MonoBehaviour, runtime asmdef)
      RewardedAdPlacementUI.cs                  ([Serializable] DTO for tester)
      UnityAdsTester.cs                         (MonoBehaviour bootstrap, runtime asmdef)
    Scaffold.Ads.LevelPlay.asmdef               (autoReferenced=true; references LevelPlay GUIDs + Scaffold.Ads + LiveOps)
  README.md, Walkthrough.md, package.json
```

The asmdef pulls in three GUID-referenced LevelPlay assemblies + `Scaffold.LiveOps` + `VContainer` + several LiveOps DTO precompiled DLLs. The LiveOps DLL list is identical to the abstraction's asmdef and should not be required here — this implementation does not call `ILiveOpsService` and never references the DTO types directly.

## What's good
- `LevelPlayAdConfigurationSO : AdConfigurationSO` (`Runtime/Core/LevelPlayAdConfigurationSO.cs:7`) is the correct extension point. `CreateProvider()` returns the concrete `LevelPlayAdProvider` (`:34-37`), so the abstraction never names LevelPlay.
- `LevelPlayInstaller` registers the SO under both the concrete and base type (`Container/LevelPlayInstaller.cs:17`) and chains `new AdsInstaller(adConfiguration).Install(builder)` (`:18`). One install call boots the whole stack — clean.
- Per-platform `LevelPlayPlatformConfig` struct (`Runtime/Core/LevelPlayPlatformConfig.cs`) is a tidy serializable shape; iOS/Android/Editor overrides slot in cleanly.
- `LevelPlayAdProvider` waits for `LevelPlay.OnInitSuccess` before constructing the three services (`Runtime/Core/LevelPlayAdProvider.cs:75-88`). Correct ordering.
- All three services implement `IDisposable` and clear their internal dictionaries (`LevelPlayBannerAdService.cs:166-173`, `LevelPlayInterstitialAdService.cs:178-182`, `LevelPlayRewardedAdService.cs:222-227`).
- Per-placement load retry with bounded attempts (`maxRetryAttempts = 3`) is applied to interstitials and rewarded (`LevelPlayInterstitialAdService.cs:12-13, 142-159`, `LevelPlayRewardedAdService.cs:12-13, 155-172`) — sensible default.
- Banner uses LevelPlay's typed `LevelPlayBannerAd.Config.Builder()` (`LevelPlayBannerAdService.cs:55`) rather than hand-rolled positioning. Idiomatic SDK use.
- A scripted bridge from `BannerPosition` enum to `LevelPlayBannerPosition` (`LevelPlayBannerAdService.cs:71-80`) keeps the SDK enum out of the abstraction.

## Issues / smells

### Threading: LevelPlay calls off the main thread
- `LevelPlayInterstitialAdService.ScheduleReloadAdInterstitial` calls `Task.Run(async () => { await Task.Delay(...); ad?.LoadAd(); })` (`Runtime/Interstitial/LevelPlayInterstitialAdService.cs:184-191`). This dispatches `LoadAd()` to a thread-pool thread. IronSource LevelPlay requires SDK calls from the Unity main thread; this will misbehave intermittently and is an Android NRE waiting to happen.
- Same bug in `LevelPlayRewardedAdService.ScheduleRewardedReload` (`Runtime/Rewarded/LevelPlayRewardedAdService.cs:184-191`).
- Use `Awaitable.WaitForSecondsAsync(retryDelaySeconds)` or schedule via the Unity main-thread synchronization context. References:
  - LevelPlay Unity SDK threading guidance: https://developers.is.com/ironsource-mobile/unity/unity-plugin/
  - Unity 6 `Awaitable.WaitForSecondsAsync` is main-thread by default: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.WaitForSecondsAsync.html

### Per-impression placement state racing
- `LevelPlayRewardedAdService.currentShowingPlacement` is a single field set in `TryShowRewarded` and read in `HandleAdDisplayed`, `HandleAdFailedToDisplay`, `HandleAdClosed`, `ProcessAdReward` (`Runtime/Rewarded/LevelPlayRewardedAdService.cs:24, 94-103, 195, 201, 215`). If a player triggers `ShowAd("A")` immediately followed by `ShowAd("B")` (or the SDK fires a delayed `OnAdRewarded` after a re-show), the placement is wrong on the reward event — and the reward server-validates the wrong placement. The `LevelPlayAdInfo.Placement` carried by every callback is the authoritative per-impression source. Use it.
- `LevelPlayInterstitialAdService` doesn't track placement during display either, but its events all carry the local `key` from the closure (`:64-69`) — that's the correct pattern. Apply the same in the rewarded path.

### Rewarded uses `unitId` as the dictionary key — placements collapse
- `LevelPlayRewardedAdService.adsByUnitId` is keyed by `unitId` (`Runtime/Rewarded/LevelPlayRewardedAdService.cs:21, 54-64`) and `placementToUnitId` maps placement → unitId (`:22`). Two placements with the same `AdUnitId` share one `LevelPlayRewardedAd` instance and one retry counter (`loadRetryCounts` is also keyed by `unitId`, `:23, 62`). That's reasonable for per-unit caching, BUT the load-failed path then invokes `AdAvailable?.Invoke(false)` (`:167`) even though some other placement on a different unit could still be available. Track availability per-unit and aggregate.
- The interstitial service does the opposite — keyed by placement (`Runtime/Interstitial/LevelPlayInterstitialAdService.cs:21-22, 41-60`). Two placements with the same unitId create two `LevelPlayInterstitialAd` instances against the same unit — unnecessary load duplication. Pick one model.

### Banner: registered but never loaded
- `LevelPlayBannerAdService.RegisterBannerPlacement` constructs the `LevelPlayBannerAd` and wires events but never calls `LoadAd()` (`Runtime/Banner/LevelPlayBannerAdService.cs:34-58`). The first `ShowBanner()` call will silently no-op until the consumer separately calls `LoadBanner()`. The contract on `IBannerAdService.ShowBanner` (`com.scaffold.ads/Runtime/Abstraction/Banner/IBannerAdService.cs:8`) doesn't say "load first". Auto-load on register, or document the requirement.
- `LoadBanner`, `ShowBanner`, `HideBanner`, `DestroyBanner` all silently no-op when `placementName` doesn't match (`LevelPlayBannerAdService.cs:82-117`, returns null `targetAd`). Caller mistype = silent failure. Throw on unknown placement.
- `GetTargetAd` with a null name returns the first dictionary entry (`:119-128`) — non-deterministic; depends on insertion order. If two placements exist, "the default" is whichever was registered first.

### Position override is silently ignored
- `LevelPlayBannerAdService.ShowBanner` accepts `BannerPosition?` and logs a warning then ignores it (`Runtime/Banner/LevelPlayBannerAdService.cs:91-102`). Either drop the parameter from the interface (`com.scaffold.ads/Runtime/Abstraction/Banner/IBannerAdService.cs:8`) or implement re-creation with the new position. A parameter that does nothing is a lie.

### Editor-config fallback on production platforms
- `LevelPlayAdConfigurationSO.GetActiveConfiguration` returns `EditorConfig` when no platform match is found (`Runtime/Core/LevelPlayAdConfigurationSO.cs:54-56`). On a real device with a missing iOS or Android entry, you ship the editor app key. That is a production incident. Throw `InvalidOperationException` naming the missing `Application.platform`.
- `LevelPlayAdProvider` reads `activeConfig` once in the ctor (`Runtime/Core/LevelPlayAdProvider.cs:12`) before any guard, so a misconfigured asset NREs at construction time, with no helpful message.

### `async void`, no cancellation, fire-and-forget
- `LevelPlayInterstitialAdService.ShowAd` is `async void` (`Runtime/Interstitial/LevelPlayInterstitialAdService.cs:71`). Same for `LevelPlayRewardedAdService.ShowAd` (`Rewarded/LevelPlayRewardedAdService.cs:76`). Throws inside become unhandled. Match the rest of the surface: at minimum push the `await CanShowAd` check to the manager and make `ShowAd` synchronous.
- `CanShowAd` uses `await Task.Yield()` (`Interstitial/.cs:102`, `Rewarded/.cs:108`) — a no-op yield purely to make the method async. Either drop the `async` and return `Awaitable.FromResult(...)`, or actually do an async readiness probe.
- `LevelPlayAdProvider.Initialize` swallows everything synchronously and `await Task.Yield();`s once (`Runtime/Core/LevelPlayAdProvider.cs:30-46`); the awaitable returns before `OnInitSuccess` fires. Callers (`AdManager.InitializeAds` at `com.scaffold.ads/Runtime/Abstraction/Core/AdManager.cs:48-53`) then immediately call `InitializeRewardedAdManager()` which reads `adProvider.RewardedAdService`, which is still `null` until the async LevelPlay callback runs. The whole "init then wire managers" sequence is racy. Use a `TaskCompletionSource<LevelPlayConfiguration>` set on `OnInitSuccess` / `OnInitFailed` and `await` that.

### Defaults that hide errors
- `LevelPlayBannerAdService.GetLevelPlayPosition` returns `LevelPlayBannerPosition.BottomCenter` for an unknown enum value (`Runtime/Banner/LevelPlayBannerAdService.cs:71-80`). `BannerPosition` is exhaustively defined in the abstraction (`com.scaffold.ads/Runtime/Abstraction/Banner/BannerPosition.cs`) — switch must be exhaustive; throw on default.
- `LevelPlayRewardedAdService.HandleAdDisplayed/HandleAdFailedToDisplay/HandleAdClosed/ProcessAdReward` all coalesce `currentShowingPlacement ?? "default"` (`Runtime/Rewarded/LevelPlayRewardedAdService.cs:195, 201, 215`) — combined with the racing problem above, a stale or null state silently produces a `"default"` placement event that the manager then treats as authoritative.
- `LevelPlayInterstitialAdService.HandleAdLoadFailed` early-returns when retry counter is missing (`:146-149`). The counter is set during `RegisterInterstitialPlacement`; if it's missing, the ad object should not exist either. This is dead-defense or a real invariant violation that should throw.
- `RegisterInterstitialPlacement` and `RegisterBannerPlacement` both silently early-return on empty `unitId` (`Interstitial/.cs:43-47`, `Banner/.cs:36-40`). A misconfigured config asset thus produces zero-ads at runtime with no warning. Log loudly or throw at config-validation time.

### Reward token is fabricated client-side
- `LevelPlayRewardedAdService.ProcessAdReward` constructs a "token" by concatenating timestamp + instance ID + ad network name + reward (`Runtime/Rewarded/LevelPlayRewardedAdService.cs:217`). This token is then sent to the LiveOps backend as proof of ad completion. It is not a signed S2S validation token and is trivially forgeable from the client. The README of the parent package promises "secure" reward validation (`com.scaffold.ads/README.md:166-170`); this implementation does not deliver that. Either:
  - Hook IronSource's S2S callback / impression-level data and surface that signed payload.
  - Document loudly that reward validation here is server-trusted-only-for-cooldown, and that reward grant decisions must not depend on the token contents.
  - References on LevelPlay rewarded callbacks: https://developers.is.com/ironsource-mobile/unity/rewarded-video-integration-unity/

### Test code in the runtime asmdef
- `Runtime/Test/BannerAdTester.cs`, `InterstitialAdTester.cs`, `RewardedAdTester.cs`, `RewardedAdPlacementUI.cs`, `UnityAdsTester.cs` all live in the `Scaffold.Ads.Levelplay.Runtime` asmdef. They are MonoBehaviours and ship into player builds. Move to `Samples~/` (so Unity Package Manager treats them as imported samples) or a separate `Scaffold.Ads.LevelPlay.Tests` asmdef with `Editor`/test platform restrictions.
- `UnityAdsTester` (`Runtime/Test/UnityAdsTester.cs`) is more than a tester — it's a bootstrap that auto-initializes the entire ad stack from `Start()` and constructs the reward endpoint client based on whether `ILiveOpsService` is `[Inject]`-able (`:24-64`). This bypasses the architect's MVVM/VContainer convention: composition root is supposed to construct and inject `IRewardEndpointClient`, not a `MonoBehaviour` named `*Tester`. If this is the intended bootstrap, name it `AdsBootstrap`, move it out of `Test/`, and document. If it's a tester, gate it with `Application.isEditor` and `[Conditional("DEVELOPMENT_BUILD")]`.

### Provider services don't dispose the LevelPlay objects
- `LevelPlayInterstitialAdService.Dispose` clears the dictionary but never disposes/destroys each `LevelPlayInterstitialAd` (`Runtime/Interstitial/LevelPlayInterstitialAdService.cs:178-182`). LevelPlay's interstitial doesn't have a destroy API per-instance, but the event handlers wired in `WireInterstitialAdEvents` are anonymous lambdas that capture the placement key — they will not be unsubscribed. On scene reload the SDK still fires into a dead service. Either store strong references to the lambdas and `-=` them in `Dispose`, or rebuild the wrappers per-init.
- Same in `LevelPlayRewardedAdService.Dispose` (`:222-227`).
- `LevelPlayBannerAdService.Dispose` does call `DestroyAd()` per banner (`:166-173`) but again the event handlers are leaked.

### Asmdef GUID references + duplicated precompiled list
- `Runtime/Scaffold.Ads.LevelPlay.asmdef` lists three `GUID:` references for LevelPlay (`Runtime/Scaffold.Ads.LevelPlay.asmdef:5-7`). Use the human-readable assembly names (`Unity.Services.LevelPlay`, etc.) so the asmdef is greppable and survives package re-imports.
- The same precompiled DLL list (`LiveOps.DTO.dll`, `Scaffold.LiveOps.Ads.DTO.dll`, `Scaffold.LiveOps.DirectPush.DTO.dll`) appears here as in the abstraction asmdef. This package never references those DTOs directly. Drop them.
- `references` includes `Scaffold.LiveOps` but no file in this assembly uses LiveOps types. Drop the reference.

## Suggested before/after

**Main-thread retry that doesn't fight the SDK.**
```csharp
// before — Runtime/Interstitial/LevelPlayInterstitialAdService.cs:184-191
private void ScheduleReloadAdInterstitial(LevelPlayInterstitialAd ad)
{
    Task.Run(async () =>
    {
        await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
        ad?.LoadAd();
    });
}

// after
private async Awaitable ScheduleReloadAdInterstitial(LevelPlayInterstitialAd ad, CancellationToken ct)
{
    await Awaitable.WaitForSecondsAsync(retryDelaySeconds, ct);
    ad?.LoadAd(); // back on main thread by default
}
```

**Use `LevelPlayAdInfo.Placement` instead of a single mutable field.**
```csharp
// before — Runtime/Rewarded/LevelPlayRewardedAdService.cs:213-220
private void ProcessAdReward(LevelPlayAdInfo adInfo, LevelPlayReward reward)
{
    string placementName = currentShowingPlacement ?? "default";
    string token = $"{DateTime.UtcNow.Ticks}_{adInfo.InstanceId}_{adInfo.AdNetwork}_{reward.Name}_{reward.Amount}";
    AdSuccessfullyCompletedWithToken?.Invoke(true, placementName, token);
    AdSuccessfullyCompleted?.Invoke(true, placementName);
}

// after — placement is whatever the SDK reports for this impression
private void ProcessAdReward(LevelPlayAdInfo adInfo, LevelPlayReward reward)
{
    string placement = string.IsNullOrEmpty(adInfo.Placement) ? null : adInfo.Placement;
    if (placement == null) throw new InvalidOperationException(
        $"LevelPlay rewarded callback fired with no placement (instanceId={adInfo.InstanceId}).");
    AdCompleted?.Invoke(new RewardedAdCompleted(
        Success: true, Placement: placement, Token: adInfo.InstanceId,
        RewardName: reward.Name, RewardAmount: reward.Amount));
}
```

**Real awaitable provider initialization.**
```csharp
// before — Runtime/Core/LevelPlayAdProvider.cs:30-46
public async Awaitable Initialize(string userId)
{
    UserId = userId;
    if (isInitialized) return;
    LevelPlay.OnInitSuccess += OnSDKInitialized;
    LevelPlay.OnInitFailed += OnSDKInitializationFailed;
    StartLevelPlaySdk(activeConfig.AppKey);
    await Task.Yield();
}

// after
public async Awaitable Initialize(string userId, CancellationToken ct = default)
{
    if (isInitialized) throw new InvalidOperationException("LevelPlay already initialized.");
    UserId = userId;
    var tcs = new TaskCompletionSource<LevelPlayConfiguration>();
    void OnOk(LevelPlayConfiguration c)        => tcs.TrySetResult(c);
    void OnFail(LevelPlayInitError e)          => tcs.TrySetException(
        new InvalidOperationException($"LevelPlay init failed: {e.ErrorMessage}"));

    LevelPlay.OnInitSuccess += OnOk;
    LevelPlay.OnInitFailed  += OnFail;
    try
    {
        StartLevelPlaySdk(activeConfig.AppKey);
        await tcs.Task.WaitAsync(ct);
        BuildServices();
        isInitialized = true;
    }
    finally
    {
        LevelPlay.OnInitSuccess -= OnOk;
        LevelPlay.OnInitFailed  -= OnFail;
    }
}
```

**Throw, don't fall back to editor config.**
```csharp
// before — Runtime/Core/LevelPlayAdConfigurationSO.cs:39-56
public LevelPlayPlatformConfig GetActiveConfiguration()
{
    if (Application.isEditor) return EditorConfig;
    foreach (var c in PlatformConfigs)
        if (c.Platform == Application.platform) return c;
    Debug.LogWarning(...);
    return EditorConfig;
}

// after
public LevelPlayPlatformConfig GetActiveConfiguration()
{
    if (Application.isEditor) return EditorConfig;
    foreach (var c in PlatformConfigs)
        if (c.Platform == Application.platform) return c;
    throw new InvalidOperationException(
        $"[LevelPlay] No platform config for {Application.platform}. " +
        $"Add an entry to LevelPlayAdConfigurationSO.PlatformConfigs.");
}
```

**Banner: auto-load on register, throw on unknown placement.**
```csharp
private LevelPlayBannerAd Require(string placementName)
    => bannerAds.TryGetValue(placementName, out var ad) ? ad
       : throw new KeyNotFoundException($"Banner placement '{placementName}' not registered.");

private LevelPlayBannerAd CreateBannerAd(BannerAdConfig p, string unitId, string key)
{
    var pos    = GetLevelPlayPosition(p.BannerPosition);
    var config = new LevelPlayBannerAd.Config.Builder().SetPosition(pos).Build();
    var ad     = new LevelPlayBannerAd(unitId, config);
    WireBannerAdEvents(ad, key);
    ad.LoadAd();   // auto-load
    return ad;
}
```

## Easy wins
1. Move all of `Runtime/Test/*` under `Samples~/` so they don't ship in player builds (`Runtime/Test/BannerAdTester.cs`, `InterstitialAdTester.cs`, `RewardedAdTester.cs`, `RewardedAdPlacementUI.cs`, `UnityAdsTester.cs`).
2. Replace `Task.Run` retry with `Awaitable.WaitForSecondsAsync` (`Runtime/Interstitial/LevelPlayInterstitialAdService.cs:184-191`, `Runtime/Rewarded/LevelPlayRewardedAdService.cs:184-191`).
3. Throw instead of falling back to `EditorConfig` on missing platform (`Runtime/Core/LevelPlayAdConfigurationSO.cs:54-56`).
4. Use `adInfo.Placement` instead of `currentShowingPlacement` in the rewarded service (`Runtime/Rewarded/LevelPlayRewardedAdService.cs:24, 94, 195, 201, 215`).
5. Auto-`LoadAd()` banners on registration (`Runtime/Banner/LevelPlayBannerAdService.cs:48-58`).
6. Drop the `BannerPosition?` parameter from `ShowBanner`, since the SDK can't honor it (`Runtime/Banner/LevelPlayBannerAdService.cs:91-102` and the corresponding `IBannerAdService.cs:8`).
7. Replace `default: return LevelPlayBannerPosition.BottomCenter` with `throw new ArgumentOutOfRangeException` (`Runtime/Banner/LevelPlayBannerAdService.cs:78`).
8. Drop dead asmdef references: `Scaffold.LiveOps` and the three LiveOps DTO precompileds (`Runtime/Scaffold.Ads.LevelPlay.asmdef:8, 16-18`). Replace LevelPlay GUID refs with assembly names.

## Bigger refactors

### Treat `LevelPlayAdInfo.Placement` as the single per-impression source of truth
Rip out `currentShowingPlacement` (`LevelPlayRewardedAdService.cs:24`). Wire each callback closure to capture `placement` (or read it from `adInfo.Placement`). This collapses the rewarded service's per-impression state into the SDK's own data and removes the rewrite-race.

### Consolidate keying strategy across the three services
Pick one: key by placement, key by adUnitId, or both with explicit indirection. Today, banner is keyed by placement (`LevelPlayBannerAdService.cs:49`), interstitial is keyed by placement (`LevelPlayInterstitialAdService.cs:21-22`), rewarded is keyed by unitId with a placement→unitId map (`LevelPlayRewardedAdService.cs:21-22`). The mismatch is a bug source. The right shape: dictionary keyed by placement key, value is `(LevelPlayXxxAd ad, string unitId, int retryCount)`; deduplicate ad objects by unitId via a secondary cache.

### Real awaitable init + provider lifecycle
Wrap `LevelPlay.OnInitSuccess`/`OnInitFailed` in a `TaskCompletionSource` so `Initialize` truly completes when init does (`LevelPlayAdProvider.cs:30-88`). Today `AdManager.InitializeAds` thinks initialization is done before the services exist, and downstream `Initialize*` calls silently no-op against null services because `AdManager` guards them defensively (`com.scaffold.ads/Runtime/Abstraction/Core/AdManager.cs:67-89`). Fixing the provider lets you delete those guards in the abstraction.

### Decouple bootstrap from `MonoBehaviour`
Move the logic in `UnityAdsTester` (`Runtime/Test/UnityAdsTester.cs`) into a `LevelPlayBootstrap` registered via VContainer's `IAsyncStartable` or `IInitializable`. The `MonoBehaviour` should not own the decision of whether to use `LiveOpsRewardEndpointClient` or `HttpRewardEndpointClient` — the installer should.

### Add a `HeadlessAdProvider` companion
Outside this package's scope but worth raising again: a fake provider in the abstraction package would let LevelPlay's services be unit-tested without the SDK present (today they cannot run under PlayMode tests without a real LevelPlay account).

## Organization & docs
- `README.md` is brief but accurate; the line "swap the asset in your manager's inspector without changing any game code!" (`README.md:20`) is a fair claim given the SO factory.
- `Walkthrough.md` references `LevelPlayAdService.cs` and `IAdService` (`Walkthrough.md:6, 10-11`) which do not exist in this package — those are stale names from a refactor. Either update or delete.
- No `CHANGELOG.md`. Unity Package Manager will surface one if present.
- `package.json:13` pins `com.unity.services.levelplay: 9.3.0` — fine, but no minimum/maximum range. Consider documenting the minimum tested version in README.
- The two asmdefs (`Runtime` + `Container`) split is consistent with `com.scaffold.ads`. Good.
- No tests. The structure makes them easy to add behind PlayMode tests once the threading is fixed.

## References
- IronSource LevelPlay Unity plugin (threading, lifecycle, callbacks): https://developers.is.com/ironsource-mobile/unity/unity-plugin/
- LevelPlay Rewarded video integration (callback contract): https://developers.is.com/ironsource-mobile/unity/rewarded-video-integration-unity/
- LevelPlay Banner integration (position constraints, single-instance behavior): https://developers.is.com/ironsource-mobile/unity/banner-integration-unity/
- Unity 6 Awaitable + main-thread guarantees: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.WaitForSecondsAsync.html
- AppLovin MAX rewarded SSV pattern (the gold standard for "don't trust client tokens"): https://developers.applovin.com/en/max/unity/ad-formats/rewarded-ads/#server-side-callbacks
- Google AdMob mediation reward verification: https://developers.google.com/admob/unity/ssv
