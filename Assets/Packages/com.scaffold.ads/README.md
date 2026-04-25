# Scaffold.Ads

Keywords: ads, rewarded, interstitial, banner, liveops, placement, cooldown

## TL;DR
- Purpose: Client-side ad orchestration — pacing, events, and reward validation via pluggable endpoint clients.
- Location: `Assets/Packages/com.scaffold.ads/`
- Depends on: `VContainer`, `Scaffold.LiveOps`, `LiveOps.DTO.dll`, `Scaffold.LiveOps.*.DTO.dll` (precompiled references from `Scaffold.LiveOps` asmdef)
- Used by: `com.scaffold.ads.levelplay` (LevelPlay provider), App-layer UI controllers
- Runtime only (no Editor tooling)

## Responsibilities
- Owns:
  - Ad initialization lifecycle (`AdManager`)
  - Specialized pacing per ad type (`RewardedAdManager`, `InterstitialAdManager`, `BannerAdManager`)
  - Reward validation dispatch (`IRewardEndpointClient` → backend)
  - Client-side cooldown tracking between ad impressions
- Does not own:
  - SDK-specific initialization (owned by `IAdProvider` implementations in `com.scaffold.ads.levelplay`)
  - Backend validation logic (owned by `AdsService` in Cloud Code)
  - Economy or currency mutations (implement in your Cloud Code modules under `LiveOps/Game/**` if needed)
- Boundaries:
  - Pure C# (no MonoBehaviours in runtime assembly)
  - Uses `UnityEngine` for logging and `UnityWebRequest` only in `HttpRewardEndpointClient`

## Architecture

### Manager of Managers Pattern

```
AdManager (gateway)
├── RewardedAdManager    → IRewardedAdService + IRewardEndpointClient
├── InterstitialAdManager → IInterstitialAdService
└── BannerAdManager      → IBannerAdService
```

### Reward Endpoint Clients

Two implementations of `IRewardEndpointClient`:

| Client | When to use | What it does |
|--------|------------|--------------|
| `LiveOpsRewardEndpointClient` | Production with LiveOps backend | Sends `WatchAdRequest` via `ILiveOpsService.CallAsync()`. Backend validates, grants reward, returns updated state. |
| `HttpRewardEndpointClient` | Legacy / custom endpoint | Sends raw HTTP POST to a configured URL with userId + adId. |

### Execution Flow (Pseudo-code)

```
┌─────────────────────────────────────────────────────────────────┐
│ CLIENT                                                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. Player taps "Watch Ad" button for placement "Main_Menu"     │
│     └─► AdRewardUIController.OnClickWatchAd()                   │
│         └─► RewardedAdManager.ClickShowAdReward("Main_Menu")    │
│                                                                 │
│  2. Manager checks local cooldown                               │
│     └─► HasCooldownExpired("Main_Menu") → true                  │
│     └─► IRewardedAdService.CanShowAd("Main_Menu") → true       │
│     └─► IRewardedAdService.ShowAd("Main_Menu")                 │
│                                                                 │
│  3. LevelPlay SDK plays video → callback fires                  │
│     └─► HandleAdSuccessfullyCompletedWithToken(true, token)     │
│         └─► Records local completion time                       │
│         └─► IRewardEndpointClient.CallRewardEndpointAsync(      │
│                 userId, "Main_Menu", token, endpointUrl)         │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│ NETWORK  →  WatchAdRequest { PlacementId = "Main_Menu" }        │
├─────────────────────────────────────────────────────────────────┤
│ BACKEND (Cloud Code)                                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  4. AdsService.WatchAd(request)                                 │
│     ├─► Load AdsConfig from RemoteConfig                        │
│     │   └─► Placements["Main_Menu"] = {                         │
│     │         CooldownSeconds: 300,                             │
│     │         MaxViews: 5,                                      │
│     │         RewardType: "",                                   │
│     │         RewardAmount: 0                                   │
│     │       }                                                   │
│     ├─► Load AdsPersistence from PlayerData                     │
│     │   └─► Placements["Main_Menu"] = {                         │
│     │         WatchCount: 2,                                    │
│     │         LastAdWatchedAtUtcUnix: 1711929600                │
│     │       }                                                   │
│     ├─► Validate:                                               │
│     │   ├─► HasReachedMaxViews(2 >= 5)? → NO                   │
│     │   └─► IsCooldownElapsed(300s)? → YES                     │
│     ├─► persistence.RecordAdWatched("Main_Menu")                │
│     │   └─► WatchCount: 3, LastWatched: now                    │
│     ├─► GrantReward (template logs if RewardType is set)        │
│     └─► Return WatchAdResponse                                  │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│ RESPONSE                                                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  WatchAdResponse {                                              │
│    StatusType: Success,                                         │
│    Data: AdData {                                               │
│      Placements["Main_Menu"] = {                                │
│        WatchCount: 3,                                           │
│        MaxViews: 5,                                             │
│        HasReachedMaxViews: false,                               │
│        CooldownSeconds: 300,                                    │
│        NextAdAvailableUtc: "2026-04-02T00:05:00Z",             │
│        RewardType: "",                                          │
│        RewardAmount: 0                                          │
│      }                                                          │
│    },                                                           │
│    Responses: [ ]                                               │
│  }                                                              │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│ CLIENT (cont.)                                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  5. response.IsSuccess() → true                                 │
│     └─► AdSuccessfullyCompleted?.Invoke(true, "Main_Menu")      │
│     └─► UI updates: button disabled / cooldown timer shown      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|--------|---------|--------|---------|-----------------|
| `AdManager.InitializeAds(userId, rewardClient)` | Bootstrap all ad types | Unity user ID, `IRewardEndpointClient` | void (async) | Logs error if `AdConfigurationSO` is null |
| `RewardedAdManager.ClickShowAdReward(placementName)` | Show a rewarded ad | Placement key (nullable) | void (async) | Logs warning if cooldown active or ad not ready |
| `RewardedAdManager.CanShowAd(placementName)` | Check availability | Placement key | `Awaitable<bool>` | Returns false if not initialized |
| `IRewardEndpointClient.CallRewardEndpointAsync(...)` | Validate reward on backend | userId, placementId, token, url | `Task<bool>` | Returns false on failure |

## Setup / Integration

1. **Register in VContainer** via `AdsInstaller`:
   ```csharp
   new AdsInstaller(adConfigurationSO).Install(builder);
   ```

2. **Choose your endpoint client**:
   - LiveOps: inject `ILiveOpsService`, pass `new LiveOpsRewardEndpointClient(liveOpsService)`
   - HTTP: use `new HttpRewardEndpointClient()` (no dependencies)

3. **Initialize**:
   ```csharp
   adManager.InitializeAds(userId, endpointClient);
   ```

4. **Configure RemoteConfig** (backend):
   ```json
   {
     "Placements": {
       "Main_Menu": {
         "CooldownSeconds": 300,
         "MaxViews": 5,
         "RewardType": "",
         "RewardAmount": 0
       }
     }
   }
   ```

## Best Practices
- Always use `LiveOpsRewardEndpointClient` in production — it validates server-side.
- Optional: set `RewardType` / `RewardAmount` only when your Cloud Code host implements matching reward logic (the template `AdsService` logs a warning if they are set).
- Keep `HttpRewardEndpointClient` only for testing or legacy endpoints.
- Don't bypass `RewardedAdManager` — it tracks cooldowns and guards double-grants.
- Let the backend be the source of truth for view limits and cooldowns; client checks are UX-only.

## Anti-Patterns
- ❌ Granting rewards client-side without backend validation → exploitable.
- ❌ Hardcoding `RewardType` strings without a matching server handler → rewards never apply.
- ❌ Calling `IRewardedAdService.ShowAd()` directly → bypasses cooldown and event routing.
- ❌ Adding MonoBehaviours to `Scaffold.Ads.Runtime` assembly → keep it pure C#.

## AI Agent Context
- Invariants:
  - `IRewardEndpointClient` must always be injected before `InitializeAds` is called.
  - `placementId` flows end-to-end: client → request → backend → persistence → response.
  - Backend is the authoritative source for MaxViews/Cooldown; client mirrors for UX.
- Allowed Dependencies:
  - `VContainer`, `Scaffold.LiveOps`, `LiveOps.DTO.dll`, `Scaffold.LiveOps.*.DTO.dll`
- Forbidden Dependencies:
  - `com.scaffold.ads.levelplay` (provider implementation must not leak into abstraction)
  - Direct references to Cloud Code economy modules from the client package
- Change Checklist:
  - If adding rewards from ads: extend `LiveOps/Scaffold/Ads/AdsService.cs` (fork) or add modules under `LiveOps/Game/**`.
  - If changing `IRewardEndpointClient`: update both `LiveOpsRewardEndpointClient` and `HttpRewardEndpointClient`.
  - Run `dotnet build` on `LiveOps/` after DTO changes.
- Known Tricky Areas:
  - `ModuleResponse.IsSuccess()` is a method, not a property — don't use `.Success`.
  - `ResponseStatusType.Success` is enum value 0 (default) — responses are "success" unless explicitly set otherwise.

## Related
- `LiveOps/Scaffold/Ads.DTO/` — Shared DTOs
- `LiveOps/Scaffold/Ads/AdsService.cs` — Backend validation
- `Assets/Packages/com.scaffold.ads.levelplay/` — LevelPlay provider implementation
- `Docs/LiveOps/LiveOps.md` — LiveOps system docs

## Changelog
- 2026-04-25: Docs updated for removal of legacy Gold/Level Cloud Code modules; template backend no longer grants gold from `AdsService`.
- 2026-04-02: Added per-placement tracking (cooldowns, maxViews, rewards). Introduced `LiveOpsRewardEndpointClient`, renamed `RewardEndpointClient` → `HttpRewardEndpointClient`. Added `RewardType`/`RewardAmount` to config and backend reward granting via `GoldModule`.
