# Ads Module Overview

## Architecture Overview
The Ads system is structured using a **Manager of Managers** pattern coupled with **Interface Segregation** and **Polymorphic Configuration** to support `Rewarded`, `Interstitial`, and `Banner` ad types interchangeably without bloated, monolithic scripts.

### 1. GlobalAdManager (The Gateway)
`GlobalAdManager.cs` acts as the primary access point for UI Controllers or flow logic. Instead of finding specialized managers, components inject this single dependency.

```csharp
public class GlobalAdManager : MonoBehaviour
{
    public RewardedAdManager RewardedAds { get; }
    public InterstitialAdManager InterstitialAds { get; }
    public BannerAdManager BannerAds { get; }
...
}
```

### 2. Specialized Ad Managers (Pacing & Event Routing)
`RewardedAdManager`, `InterstitialAdManager`, and `BannerAdManager` attach alongside the Global Manager. Their responsibility is strictly logical: they track cooldowns (e.g. 5 seconds between interstitials), route specific callbacks (like firing off webhook endpoints for tokens), and check if the ad is ready to display before passing the command to the Provider Layer.

### 3. IAdProvider and Specialized Interfaces
The `IAdProvider` handles the SDK initialization logic (like passing a specific App Key to IronSource) and then dynamically instantiates internal classes that conform to:
*   `IRewardedAdService`
*   `IInterstitialAdService`
*   `IBannerAdService`

This decoupled interface means the *Game Logic* never knows it's using IronSource, AdMob, or Unity Ads. It simply asks `adManager.RewardedAds.ShowAd()`.

### 4. Polymorphic Configurations
Scriptable objects map the keys locally.
`LevelPlayAdConfigurationSO` stores specific separated lists per platform:
*   `List<RewardedAdConfig> rewardedPlacements`
*   `List<InterstitialAdConfig> interstitialPlacements`
*   `List<BannerAdConfig> bannerPlacements`
*   `LevelPlayPlatformConfig editorConfig`

This allows specific fields (like `BannerPosition` or `rewardEndpointUrl`) to only exist on the configurations that need them, avoiding messy structs.

## Integration Walkthrough

### Setting up a new Ad Type implementation

**1. Create your Provider**
If you want to migrate off IronSource LevelPlay, create a new class implementing `IAdProvider`:
```csharp
public class AdMobAdProvider : IAdProvider 
{ 
    public IRewardedAdService RewardedAdService => _rewardedService;
    // ...
}
```

**2. Create your Services**
Implement the specific Ad Services tracking the specific SDK callbacks.
```csharp
public class AdMobRewardedAdService : IRewardedAdService 
{
    // Wrap the AdMob SDK events to fire IRewardedAdService events
    public event Action<bool, string, string> AdSuccessfullyCompletedWithToken;
}
```

**3. Update Initialization**
Inside `GlobalAdManager.InitializeAds()`, swap out `_adProvider = new LevelPlayAdProvider(...)` for your new `AdMobAdProvider`.

### Testing in the Editor

1. Open the `UnityAdsExample` component.
2. Link the `GlobalAdManager` directly referencing the updated Prefab.
3. Call `adManager.RewardedAds.ClickShowAdReward("Optional_Placement_Name")` from inside a button trigger.
4. Provide an `editorConfig` in the inspector of your `LevelPlayAdConfigurationSO` so it overrides iOS/Android fallbacks safely inside the Unity Editor block!
