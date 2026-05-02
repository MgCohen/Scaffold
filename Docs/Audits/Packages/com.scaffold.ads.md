# com.scaffold.ads — Audit

## Summary
The provider-agnostic ad layer. Three managers (`RewardedAdManager`, `InterstitialAdManager`, `BannerAdManager`) sit behind an `AdManager` gateway and consume three thin service interfaces. A `ScriptableObject` factory (`AdConfigurationSO`) builds the active `IAdProvider`. Reward validation is delegated through `IRewardEndpointClient` with an HTTP and a LiveOps implementation.

The abstraction is genuinely clean of provider types — no `Unity.Services.LevelPlay` or AdMob symbols leak in. The factory pattern (`AdConfigurationSO.CreateProvider()`) is sound and matches the rubric for a known-pluggable surface. But the surface is undermined by `async void` everywhere, no cancellation, no result types, weak-typed `string` placement keys, defensive guards smeared through the call chain, and silent fallbacks that hide errors. There are no test fakes, no headless provider, and a sample `MonoBehaviour` (`AdRewardUIController`) sits inside the abstraction package which the README forbids.

**Verdict: refactor.** The shape is right; the API contract needs tightening.

## Structure
```
com.scaffold.ads/
  Container/
    AdsInstaller.cs                                  (VContainer registrations)
    Scaffold.Ads.Container.asmdef
  Runtime/
    Abstraction/
      Core/
        AdManager.cs                                  (gateway)
        AdPlacementConfig.cs                          (base data)
        AdPlacementKeySO.cs                           (typed placement key SO)
        IAdProvider.cs                                (Awaitable-based provider contract)
      Configurations/
        AdConfigurationSO.cs                          (abstract SO factory)
      Banner/
        BannerAdConfig.cs, BannerAdManager.cs,
        BannerPosition.cs, IBannerAdService.cs
      Interstitial/
        InterstitialAdConfig.cs, InterstitialAdManager.cs,
        IInterstitialAdService.cs
      Rewarded/
        RewardedAdConfig.cs, RewardedAdManager.cs,
        IRewardedAdService.cs, IRewardEndpointClient.cs,
        RewardRequestPayload.cs, AdRewardUIController.cs (MonoBehaviour — wrong place)
    Implementation/
      Rewarded/
        HttpRewardEndpointClient.cs
        LiveOpsRewardEndpointClient.cs
    Scaffold.Ads.asmdef                               (autoReferenced=true, references VContainer + Scaffold.LiveOps)
  Backend~/Scaffold/
    Ads.DTO/  (AdData, AdPlacementConfig, AdPlacementState, AdsConfig, AdsPersistence,
               Request/WatchAdRequest, Request/WatchAdResponse)
    Ads/AdsService.cs                                 (Cloud Code GameModule)
  README.md, package.json, Runtime/README.md, Runtime/Walkthrough.md
```

`Backend~/` is correctly tilde-suppressed from Unity import. `Scaffold.Ads.Container.asmdef` is one tiny installer in its own assembly — fine. `Implementation/` directory only holds `Rewarded/`; `Banner/` and `Interstitial/` have no in-package implementation (lives in the LevelPlay package), which leaves a one-sided shape.

## What's good
- Provider abstraction is **clean of provider types**. `IAdProvider`, `IRewardedAdService`, `IInterstitialAdService`, `IBannerAdService`, the configs and managers all stay free of LevelPlay/AdMob symbols. Only `UnityEngine.Awaitable` leaks (acceptable — Unity 6 native primitive).
- The SO-as-factory pattern (`AdConfigurationSO.CreateProvider()` at `Runtime/Abstraction/Configurations/AdConfigurationSO.cs:38`) cleanly inverts the dependency: the abstraction package never references the implementation, and selecting a different provider is a single asset swap. This is the right abstraction at the right layer.
- Reward validation is correctly factored out behind `IRewardEndpointClient` (`Runtime/Abstraction/Rewarded/IRewardEndpointClient.cs:5-8`) with two real implementations. Backend is treated as the source of truth (README:165-170). Good security posture.
- Three formats are separated into three managers with three service interfaces — no combined "AdService" god-object.
- `AdPlacementKeySO` (`Runtime/Abstraction/Core/AdPlacementKeySO.cs`) gives placement keys a typed identity in the inspector, with implicit `string` conversion for ergonomics. Right call.
- `AdManager` implements `IDisposable` and forwards to the provider (`Runtime/Abstraction/Core/AdManager.cs:91-94`). Each manager unsubscribes from the service in `Dispose()` (e.g. `Runtime/Abstraction/Banner/BannerAdManager.cs:73-79`).
- VContainer registration is minimal and `AsImplementedInterfaces()` for `AdManager` is correctly applied (`Container/AdsInstaller.cs:23-25`).
- Backend DTOs are well-shaped: `AdData` is computed from `AdsPersistence` + `AdsConfig`, `AdPlacementClientData.IsAdAvailable()` and `GetRemainingCooldown()` give the client a server-authoritative view (`Backend~/Scaffold/Ads.DTO/AdPlacementClientData.cs:17-42`).

## Issues / smells

### Provider-agnosticism is mostly clean — except where it isn't
- `IAdProvider.Initialize` returns `UnityEngine.Awaitable` (`Runtime/Abstraction/Core/IAdProvider.cs:8`). `Awaitable` is fine for Unity main-thread work, but it forces every consumer onto Unity. If you ever want to test the abstraction headless under plain xUnit, this hurts. `IRewardedAdService.CanShowAd` and `IInterstitialAdService.CanShowAd` have the same problem (`IRewardedAdService.cs:8`, `IInterstitialAdService.cs:8`). Pick a lane: either commit to `Awaitable` and document, or use `ValueTask`/`Task` for portability.
- `IAdProvider.UserId { get; set; }` (`IAdProvider.cs:11`) — a public mutable provider property leaks state ownership into the interface. `userId` is already passed to `Initialize(string)`. Drop the setter; if a getter is required for diagnostics, expose read-only.
- `IAdProvider.SetMuted(bool)` (`IAdProvider.cs:9`) is in the contract but the LevelPlay implementation no-ops it (`com.scaffold.ads.levelplay/Runtime/Core/LevelPlayAdProvider.cs:48-51`). Either delete from interface (no-one calls it, no other provider) or push it down to a capability interface (`IMutableAudio`).

### `async void` and missing cancellation
- `AdManager.InitializeAds` is `async void` (`Runtime/Abstraction/Core/AdManager.cs:38`). Caller cannot await initialization or observe failure; exceptions become unhandled. This is the entry point — it must return `Awaitable` or `Task`.
- `RewardedAdManager.ClickShowAdReward` is `async void` (`Runtime/Abstraction/Rewarded/RewardedAdManager.cs:43`). Same problem.
- `InterstitialAdManager.ShowInterstitial` is `async void` (`Runtime/Abstraction/Interstitial/InterstitialAdManager.cs:32`). Same.
- `RewardedAdManager.HandleAdSuccessfullyCompletedWithToken` is `async void` and reaches the network (`RewardedAdManager.cs:98-112`). If `CallRewardEndpointAsync` throws, the process crashes on Unity's sync context — fail-fast in the worst possible place. Should be `async Awaitable` or guarded.
- No `CancellationToken` anywhere in the abstraction. `IRewardEndpointClient.CallRewardEndpointAsync` takes no token (`IRewardEndpointClient.cs:7`), so a player navigating away from the reward UI cannot cancel an in-flight validation. Add `CancellationToken cancellationToken = default` to all async surface.

### Result types vs. raw `bool` / event callbacks
- `IRewardEndpointClient.CallRewardEndpointAsync` returns `Task<bool>` (`IRewardEndpointClient.cs:7`). On failure, the caller has no idea whether it was network, server-side cooldown, max-views, or an auth error. The response from `LiveOpsRewardEndpointClient` is already richer (`WatchAdResponse` carries `AdData`) — collapsing to `bool` discards information. Return a `RewardResult` record (success, failure reason, cooldown info, watch counts).
- Events `AdSuccessfullyCompleted(bool, string)` and `AdSuccessfullyCompletedWithToken(bool, string, string)` (`IRewardedAdService.cs:11-14`) — overload by adding a string parameter is a smell. Prefer one event with a `RewardedAdCompleted` payload (`Placement`, `Token`, `Success`, `RewardName`, `RewardAmount`) and stop firing both.
- `IInterstitialAdService.AdAvailable(bool)` and `AdSuccessfullyCompleted(bool, string)` — no failure reason. Add `enum AdFailure { None, NoFill, NoNetwork, Timeout, DisplayFailed }` and surface it.

### Redundant guard clauses (rubric: entry-point only)
- `BannerAdManager.LoadBanner/ShowBanner/HideBanner/DestroyBanner` each repeats `if (!isInitialized || adService == null)` (`Runtime/Abstraction/Banner/BannerAdManager.cs:29-32, 40-43, 50-53, 60-63`). Either fail-fast in `Initialize` (which already throws-on-call by design) or check once in a `RequireReady()` helper. The pattern says "I'm scared", which is what fail-fast removes.
- `InterstitialAdManager.CanShowAd:48-52` repeats the check. So does the chain in `Show`-then-`CanShow`-then-show.
- `RewardedAdManager.TryValidateAdServiceReady` (`RewardedAdManager.cs:76-91`) checks `isInitialized` AND `adService == null`. Setting `isInitialized = true` only happens after `adService = rewardedAdService` (`:28-35`), so `adService == null` while initialized is impossible. Drop the second check.
- `RewardedAdManager.SetRewardEndpointClient` is double-guarded by `AdManager.SetRewardEndpointClient` (`AdManager.cs:56-65`): it null-checks `rewardedAdManager` (constructor-injected, never null) and `rewardClient` (already validated). Pure ceremony.
- `AdManager.InitializeRewardedAdManager`, `InitializeInterstitialAdManager`, `InitializeBannerAdManager` each guard `adProvider.X != null` (`AdManager.cs:67-89`) — but the provider is the one that owns those services, so a null is a contract violation. Throw or assert; don't silently skip.

### Default values masking errors
- `AdConfigurationSO.GetRewardedPlacement` returns `placement = null` and `false` (`Runtime/Abstraction/Configurations/AdConfigurationSO.cs:30-31`). Caller must remember to check the bool. A typed `RewardedAdConfig?` (nullable) or a `TryGet`-with-throw on misuse is clearer.
- `AdPlacementKeySO.implicit operator string` returns `string.Empty` when null (`Runtime/Abstraction/Core/AdPlacementKeySO.cs:18`). Empty string then flows through `RewardedAdManager.ClickShowAdReward` and silently becomes `"default"` (`RewardedAdManager.cs:45`). Two layers of defaulting hides the missing reference. Throw, don't substitute.
- `RewardedAdManager`/`InterstitialAdManager` substitute `defaultPlacementKey = "default"` for null (`RewardedAdManager.cs:9, 45, 170, 182, 195`). Then the backend `AdsConfig.GetPlacement` does the same on the server (`Backend~/Scaffold/Ads.DTO/AdsConfig.cs:18`), and so does `AdData.GetPlacementData` (`AdData.cs:49`), and so does `WatchAdRequest` handling (`Backend~/Scaffold/Ads/AdsService.cs:46`). Five layers of "default" coalescing — a typo in placement name silently routes to the default placement. Fail-fast at the top entry (`ClickShowAdReward`) on null/empty.
- `HttpRewardEndpointClient.CallRewardEndpointAsync` swallows the entire request exception space (`Runtime/Implementation/Rewarded/HttpRewardEndpointClient.cs:26-30`) and returns `false`. The caller cannot distinguish "endpoint is wrong" from "no internet". At least log structured.
- `HttpRewardEndpointClient` returns `true` for any `2xx-or-3xx-or-protocol-OK` (`HttpRewardEndpointClient.cs:54-60`) — does not parse the body. The endpoint could return `{"valid":false}` and the client treats it as success. This is the inverse of fail-fast.
- `AdConfigurationSO.GetRewardedPlacement` does linear scan with case-sensitive `==` against placement key (`AdConfigurationSO.cs:18-29`); if you rename the asset and forget to update `Id`, no match, returns false, ad doesn't play. A dictionary built once on enable would be O(1) and a missing key would surface in editor when the SO is loaded.
- `LevelPlayAdConfigurationSO.GetActiveConfiguration` falls back to `EditorConfig` when no platform matches (`com.scaffold.ads.levelplay/Runtime/Core/LevelPlayAdConfigurationSO.cs:54-56`). On device with a missing platform entry, you ship the editor app key. That's a launch-day incident waiting to happen — throw.

### Unity / pure-C# boundary leaks (rubric: keep them at separate boundaries)
- `AdRewardUIController` is a `MonoBehaviour` living in the abstraction runtime (`Runtime/Abstraction/Rewarded/AdRewardUIController.cs:7`). The package's own README:24-25 says "Pure C# (no MonoBehaviours in runtime assembly)". Move to a Samples~ folder or a separate `com.scaffold.ads.ui` package.
- `Scaffold.Ads.Runtime` asmdef sets `noEngineReferences=false` and depends on `UnityEngine.Awaitable` and `UnityEngine.Debug` everywhere. The runtime is not pure C# even ignoring `AdRewardUIController` — it can't be, given `Awaitable`. The README claim is aspirational. Either fix the README or split the abstraction into a pure-C# core and a Unity adapter.
- `RewardRequestPayload` (`Runtime/Abstraction/Rewarded/RewardRequestPayload.cs`) is only used by `HttpRewardEndpointClient`. Move it next to the implementation — the abstraction shouldn't host an implementation's wire format.

### Concurrency and races
- `LevelPlayInterstitialAdService.ScheduleReloadAdInterstitial` and the rewarded equivalent fire-and-forget a `Task.Run` from a Unity callback (`com.scaffold.ads.levelplay/Runtime/Interstitial/LevelPlayInterstitialAdService.cs:184-191`, `Rewarded/LevelPlayRewardedAdService.cs:184-191`). LevelPlay SDK calls (`ad?.LoadAd()`) are required to run on the Unity main thread per IronSource docs. This will sporadically misbehave or crash. Use `Awaitable.WaitForSecondsAsync()` or a Unity `MonoBehaviour` PlayerLoop hook instead of `Task.Delay`+`Task.Run`. Source: IronSource LevelPlay docs require SDK calls from the Unity main thread (see https://developers.is.com/ironsource-mobile/unity/unity-plugin/).
- `RewardedAdManager.lastAdToken` is a single field (`RewardedAdManager.cs:14`) shared across placements. Two placements completing back-to-back can race; placement B's reward is granted with placement A's token. Token should be carried inside the event payload (and inside `TryCompleteRewardEndpoint`'s closure), not stored as instance state.

### Per-impression state in the wrong place
- `LevelPlayRewardedAdService.currentShowingPlacement` is a single mutable string used to thread placement info from `Show` through to `OnAdRewarded`/`OnAdClosed` (`com.scaffold.ads.levelplay/Runtime/Rewarded/LevelPlayRewardedAdService.cs:24, 94, 195, 201, 204, 210, 215`). Two parallel rewarded surfaces or a quick re-show after close will corrupt it. LevelPlay's `LevelPlayAdInfo.Placement` should be the authoritative source per impression.

### Weak typing on placement keys
- All public APIs accept `string placementName = null` (`IBannerAdService.cs:7-10`, `IInterstitialAdService.cs:8-9`, `IRewardedAdService.cs:8-9`, every manager). The whole point of `AdPlacementKeySO` is compile-time identity; the API should accept it (with implicit `string` cast already in place, this is non-breaking).

### Cooldown duplication
- Client-side cooldown (`RewardedAdManager.rewardCooldownSeconds = 10`, `InterstitialAdManager.interstitialCooldownSeconds = 5`) is hardcoded const (`RewardedAdManager.cs:8`, `InterstitialAdManager.cs:8`) while the server already tracks `CooldownSeconds` per placement in `AdPlacementConfig` (`Backend~/Scaffold/Ads.DTO/AdPlacementConfig.cs:7`). Two sources of truth, neither read from `AdConfigurationSO`. Pull cooldown from config or from the last `AdData` response.
- `AdManager.Initialize*` methods pass `adConfiguration` to each manager, but `BannerAdManager.Initialize(IBannerAdService, AdConfigurationSO _)` and `InterstitialAdManager.Initialize(IInterstitialAdService, AdConfigurationSO _)` discard it (`BannerAdManager.cs:13`, `InterstitialAdManager.cs:17`). Either use it (read cooldowns/positions from config) or drop the parameter.

### No headless / fake provider
- There is no `NoOpAdProvider`, no `FakeRewardedAdService`, no editor-only test double. `IAdProvider` exists, but you cannot run the game with ads-on-fake in CI or in a local dev build without a key. Add an in-package `Scaffold.Ads.Headless` (or a `HeadlessAdProvider` in `Implementation/`) that always returns "not available" (or always succeeds for QA). This is the cheapest test surface in the codebase to add, and the absence is the biggest gap given the rubric.

### Tests
- Zero test assemblies. Zero `*.Tests.asmdef`. The interfaces are clean enough to fake; the lack of tests is purely a will-to-write issue.

## Suggested before/after

**Fail-fast at entry, drop downstream guards.**
```csharp
// before — Runtime/Abstraction/Banner/BannerAdManager.cs:27-66
public void LoadBanner(string placementName = null)
{
    if (!isInitialized || adService == null)
    {
        Debug.LogWarning("BannerAdManager not initialized");
        return;
    }
    adService.LoadBanner(placementName);
}
public void ShowBanner(string placementName = null, BannerPosition? position = null)
{
    if (!isInitialized || adService == null) return;
    adService.ShowBanner(placementName, position);
}
public void HideBanner(string placementName = null) { /* same guard */ }
public void DestroyBanner(string placementName = null) { /* same guard */ }

// after
public void LoadBanner(AdPlacementKeySO key)    => Service().LoadBanner(key);
public void ShowBanner(AdPlacementKeySO key, BannerPosition? position = null)
                                                => Service().ShowBanner(key, position);
public void HideBanner(AdPlacementKeySO key)    => Service().HideBanner(key);
public void DestroyBanner(AdPlacementKeySO key) => Service().DestroyBanner(key);

private IBannerAdService Service()
    => adService ?? throw new InvalidOperationException(
        "BannerAdManager.Initialize was not called before use.");
```

**Async API with cancellation, awaitable initialization, no `async void`.**
```csharp
// before — Runtime/Abstraction/Core/AdManager.cs:38-54
public async void InitializeAds(string userId, IRewardEndpointClient rewardClient)
{
    if (isInitialized) return;
    isInitialized = true;
    SetRewardEndpointClient(rewardClient);
    await adProvider.Initialize(userId);
    InitializeRewardedAdManager();
    InitializeInterstitialAdManager();
    InitializeBannerAdManager();
}

// after
public async Awaitable InitializeAds(
    string userId,
    IRewardEndpointClient rewardClient,
    CancellationToken ct = default)
{
    if (isInitialized) throw new InvalidOperationException("Ads already initialized.");
    rewardedAdManager.SetRewardEndpointClient(rewardClient);
    await adProvider.Initialize(userId, ct);
    rewardedAdManager.Initialize(adProvider.RewardedAdService, adConfiguration, userId);
    interstitialAdManager.Initialize(adProvider.InterstitialAdService);
    bannerAdManager.Initialize(adProvider.BannerAdService);
    isInitialized = true;
}
```

**Result type instead of `bool`.**
```csharp
public readonly record struct RewardResult(
    bool Granted,
    string Placement,
    RewardFailureReason Failure,
    int? UpdatedWatchCount,
    DateTime? NextAvailableUtc);

public enum RewardFailureReason { None, Network, MaxViewsReached, OnCooldown, ServerRejected, Cancelled }

public interface IRewardEndpointClient
{
    Awaitable<RewardResult> CallRewardEndpointAsync(
        string unityUserId, AdPlacementKeySO placement, string rewardAdId,
        CancellationToken ct = default);
}
```

**Typed placement keys at the API boundary.**
```csharp
// before — Runtime/Abstraction/Rewarded/IRewardedAdService.cs:8-9
Awaitable<bool> CanShowAd(string placementName = null);
void ShowAd(string placementName = null);

// after
Awaitable<bool> CanShowAd(AdPlacementKeySO key, CancellationToken ct = default);
void ShowAd(AdPlacementKeySO key);
```

**Headless provider so tests + early scenes work without an SDK.**
```csharp
public sealed class HeadlessAdProvider : IAdProvider
{
    public string UserId { get; private set; }
    public IRewardedAdService RewardedAdService { get; } = new HeadlessRewardedAdService();
    public IInterstitialAdService InterstitialAdService { get; } = new HeadlessInterstitialAdService();
    public IBannerAdService BannerAdService { get; } = new HeadlessBannerAdService();

    public Awaitable Initialize(string userId, CancellationToken ct = default)
    {
        UserId = userId;
        return AwaitableUtility.CompletedTask;
    }
    public void Dispose() { }
}
```

## Easy wins
1. Drop the duplicate `if (!isInitialized || adService == null)` guards across the three managers; centralize in a single `Service()` accessor that throws (`BannerAdManager.cs:29, 40, 50, 60`, `InterstitialAdManager.cs:48`, `RewardedAdManager.cs:76-91`).
2. Convert `AdManager.InitializeAds`, `RewardedAdManager.ClickShowAdReward`, `InterstitialAdManager.ShowInterstitial` from `async void` to `async Awaitable` (`AdManager.cs:38`, `RewardedAdManager.cs:43`, `InterstitialAdManager.cs:32`).
3. Add `CancellationToken ct = default` to `IRewardEndpointClient.CallRewardEndpointAsync` and the `CanShowAd` interfaces (`IRewardEndpointClient.cs:7`, `IRewardedAdService.cs:8`, `IInterstitialAdService.cs:8`).
4. Move `AdRewardUIController` out of the runtime asmdef into a `Samples~/` or a `Scaffold.Ads.UI` assembly (`Runtime/Abstraction/Rewarded/AdRewardUIController.cs`).
5. Move `RewardRequestPayload` next to `HttpRewardEndpointClient` — it's a wire-format DTO for one impl (`Runtime/Abstraction/Rewarded/RewardRequestPayload.cs`).
6. Delete unused `SetMuted(bool)` from `IAdProvider` (`IAdProvider.cs:9`); LevelPlay's no-op proves nobody uses it.
7. Replace per-call `string placementName = null` with `AdPlacementKeySO` overloads (it already implicit-casts to `string`, so the change is additive).
8. Pull `rewardCooldownSeconds`/`interstitialCooldownSeconds` consts out of the managers and read from `AdConfigurationSO` (`RewardedAdManager.cs:8`, `InterstitialAdManager.cs:8`).

## Bigger refactors

### Unify completion events behind a payload type
Today `IRewardedAdService` fires `AdSuccessfullyCompletedWithToken` AND `AdSuccessfullyCompleted` (`IRewardedAdService.cs:12-14`); `RewardedAdManager` subscribes to both (`RewardedAdManager.cs:32-33`) and the LevelPlay impl raises both back-to-back (`LevelPlayRewardedAdService.cs:218-219`). Listeners get double-notified. Replace with one event:

```csharp
public readonly record struct RewardedAdCompleted(
    bool Success, AdPlacementKeySO Placement, string Token, string RewardName, long RewardAmount);

public interface IRewardedAdService : IDisposable
{
    Awaitable<bool> CanShowAd(AdPlacementKeySO key, CancellationToken ct = default);
    void ShowAd(AdPlacementKeySO key);
    event Action<bool> AdAvailable;
    event Action<RewardedAdCompleted> AdCompleted;
}
```

### Headless provider + a fake `IRewardEndpointClient`
Add `HeadlessAdProvider`, `FakeRewardEndpointClient` (always-success/configurable), and a `Scaffold.Ads.Tests.asmdef`. Wire one assembly's worth of unit tests around `RewardedAdManager` to cover cooldown, double-grant, missing user ID, missing endpoint, retry semantics. The interfaces are clean enough that this is a half-day, not a week.

### Pull the abstraction off `UnityEngine.Awaitable`
Promote the contracts to pure C# `ValueTask` so the managers can be unit-tested under xUnit without Unity test runner. Provide a thin `Awaitable` adapter inside the LevelPlay package. This unblocks CI and matches the README's "pure C#" claim.

### Stop coalescing missing placements to `"default"`
The five-layer "default" fallback (client `RewardedAdManager.cs:45, 170, 195`; backend `AdsConfig.cs:18`, `AdData.cs:49`, `AdsService.cs:46`) hides typos. Throw `ArgumentNullException` at the client entry, validate placement existence against `AdConfigurationSO` once at init, and let the backend fail-loud on unknown placements.

### Push `LevelPlayAdConfigurationSO` editor fallback off the cliff
`GetActiveConfiguration` returning `EditorConfig` on a release platform (`LevelPlayAdConfigurationSO.cs:54-56`) is a production hazard. Throw, with a clear message naming the missing platform.

## Organization & docs
- Two `README.md` files (`com.scaffold.ads/README.md` and `com.scaffold.ads/Runtime/README.md`) — the second is empty/stub-likely-redundant. Consolidate.
- `Runtime/Walkthrough.md` references `IAdService.cs` and `AdRewardManager` (`Walkthrough.md:6, 9, 11`) which don't exist under those names — the file is stale by at least one rename. The current names are `IRewardedAdService` and `RewardedAdManager`. Either fix or delete.
- Backend section in `Backend~/Scaffold/Ads.DTO/Request/WatchAdRequest.cs:8` has bizarre formatting (`{        public string PlacementId`). Cosmetic.
- README is otherwise excellent — explicit responsibilities, allowed/forbidden dependencies, anti-patterns, change-checklist, AI agent context. This is the model other packages should follow.
- No CHANGELOG.md (the README has a "Changelog" section, which is fine, but Unity Package Manager surfaces a `CHANGELOG.md` automatically — add one).
- No `Tests~/` or test asmdef. Given the package's role (gameplay-critical revenue path), this is the most valuable single thing to add.
- The `Backend~` tilde-suppression is correctly applied — those files compile under the LiveOps Cloud Code project, not Unity. Good hygiene.

## References
- IronSource LevelPlay Unity SDK — main-thread requirement and lifecycle: https://developers.is.com/ironsource-mobile/unity/unity-plugin/
- Unity Awaitable best practices (Unity 6) — https://docs.unity3d.com/6000.0/Documentation/Manual/async-await-support.html
- Google AdMob mediation — typical abstraction patterns separate `IAdLoader` / `IAdRenderer` per format and surface a `LoadAdResult`/`ShowAdResult`: https://developers.google.com/admob/unity/quick-start
- AppLovin MAX abstraction guidance — single `IAdsManager` with format-specific events is the dominant industry pattern, and it's what this package effectively implements; the deltas above (result types, cancellation, typed keys) are what the mature SDKs (e.g. AppLovin's own MaxSdk) ship.
