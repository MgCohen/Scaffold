# Ads Core Runtime

## Architecture Overview
The `Runtime` module forms the core Abstraction layer of the Ads System. It handles game-agnostic and SDK-agnostic orchestration of Rewarded Ads.

By depending on this package, your game logic does not need to know which actual Ad SDK (Unity Ads, IronSource LevelPlay, AppLovin, etc.) is fulfilling the ads behind the scenes.

It includes:
1. **`Abstraction`**: Interfaces (`IAdService`, `IRewardEndpointClient`), settings definitions (`AdConfigurationSO`), and the UI controllers (`AdRewardManagerClient`, `AdRewardUIController`).
2. **`Implementation/Endpoint`**: A generic HTTP `RewardEndpointClient` implementation using `UnityWebRequest` that safely validates watch-completion tokens against your backend.

---

## Practical Usage Guide

### 1. Attaching the Manager
1. In your Main Menu or Base Scene, create an empty GameObject or use an existing manager object.
2. Attach the **`AdRewardManagerClient`** component to it.
3. Assign a concrete configuration `ScriptableObject` (provided by a separate Implementation package like LevelPlay) to the `AdConfiguration` slot in the Inspector.

### 2. Integrating with UI
You can either use the `AdRewardManagerClient` directly (via `CanShowAd()` and `ClickShowAdReward()`) or simply drag and drop the provided `AdRewardUIController`.

1. Attach **`AdRewardUIController`** to your Reward UI Button GameObject.
2. Assign the **Button** component and the **AdRewardManagerClient** reference in the Inspector.
3. Define the `Placement Name` (e.g., "Menu_Chest") for tracking context.
4. The controller will automatically handle availability checks, cooldown pacing, and button interactor states natively!

### 3. Injecting Backend Dependencies (The "Glue")
Because the `AdRewardManagerClient` is completely decoupled from Unity's Authentication SDK and other monolithic logic, you must inject the dependencies from your core Game/App system startup script.

```csharp
using Game.Ads;
using Game.Ads.Endpoint;
using UnityEngine;
using System.Threading.Tasks;

public class GameAppStartup : MonoBehaviour
{
    public AdRewardManagerClient adManager;

    private async void Start()
    {
        // 1. Wait for player login via AuthenticationService or Custom backend
        string userId = await MyAuthSystem.LoginAndGetUserId();
        
        // 2. Inject the generic HTTP Endpoint client (or your custom Cloud Code implementation)
        var rewardEndpointClient = new RewardEndpointClient();
        adManager.SetRewardEndpointClient(rewardEndpointClient);
        
        // 3. Inject the resolved User ID
        adManager.SetUnityUserId(userId);
        
        // 4. Finally, initialize the Ads System
        adManager.InitializeAds();
    }
}
```
