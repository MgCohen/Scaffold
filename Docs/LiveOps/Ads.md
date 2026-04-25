# LiveOps Ads Module

Keywords: ads, cloud-code, placement, cooldown, max-views, reward, server-validation

## TL;DR
- Purpose: Server-side validation of ad watches — enforces cooldowns, view limits, and records per-placement state (optional `RewardType` / `RewardAmount` are validated only; built-in economy dispatch was removed with the legacy Gold module).
- Location: `LiveOps/Scaffold/Ads/` (backend) + `LiveOps/Scaffold/Ads.DTO/` (shared DTOs)
- Depends on: **GameApi** (`IGameApiHandler<WatchAdRequest, WatchAdResponse>`); game-specific reward modules via **`IGameSetup`** if you extend the host
- Used by: `Scaffold.Ads` client package via `ILiveOpsService.CallAsync()`
- Runs on Unity Cloud Code (server-side C#)

## Responsibilities
- Owns:
  - Server-side validation of ad watch requests (cooldown + max views)
  - Logging when a placement configures `RewardType` / `RewardAmount` but no handler grants them (extend the Cloud Code host for real rewards)
  - Building `AdData` payloads for the client (merged config + persistence)
  - Persisting per-placement watch state (`AdsPersistence`)
- Does not own:
  - Client-side ad SDK integration (owned by `com.scaffold.ads.levelplay`)
  - Economy or progression rewards (add a `LiveOps/Game/**` module or custom `GrantReward` logic)
  - RemoteConfig schema management (ops/dashboard concern)

## DTO Breakdown

### Configuration (RemoteConfig)

#### `AdPlacementConfig`
Per-placement rules set remotely. One entry per placement ID.

```
AdPlacementConfig
├── CooldownSeconds: float  (default: 30)    → min seconds between watches
├── MaxViews: int            (default: 1)     → total allowed watches
├── RewardType: string       (default: "")    → reserved for game-specific reward wiring (template logs a warning if set)
└── RewardAmount: long       (default: 0)     → amount to grant
```

#### `AdsConfig : IGameModuleData`
Container for all placements. Key: `"AdsConfig"`.

```
AdsConfig
└── Placements: Dictionary<string, AdPlacementConfig>
    ├── "Main_Menu" → { Cooldown: 300, MaxViews: 5, RewardType: "", RewardAmount: 0 }
    └── "Level_End" → { Cooldown: 60, MaxViews: 10, RewardType: "", RewardAmount: 0 }
```

### Persistence (Player Save)

#### `AdPlacementState`
Per-placement player data. Auto-created on first watch.

```
AdPlacementState
├── LastAdWatchedAtUtcUnix: long   → unix timestamp of last watch
└── WatchCount: int                → total watches so far
```

#### `AdsPersistence : IGameModuleData`
Container for all placement states. Key: `"AdsPersistence"`.

```
AdsPersistence
└── Settings: Dictionary<string, AdPlacementState>
    Methods:
    ├── GetOrCreateState(placementId) → creates entry if missing
    ├── IsCooldownElapsed(placementId, cooldownSeconds) → bool
    ├── HasReachedMaxViews(placementId, maxViews) → bool
    ├── RecordAdWatched(placementId) → increments count + timestamp
    └── ComputeNextAdAvailableUtcIso(placementId, cooldownSeconds) → ISO string
```

### Client Payload

#### `AdPlacementClientData`
Merged view of config + persistence, sent to the client.

```
AdPlacementClientData
├── CooldownSeconds: float
├── NextAdAvailableUtc: string     → ISO 8601 UTC, empty if available now
├── MaxViews: int
├── WatchCount: int
├── HasReachedMaxViews: bool
├── RewardType: string
├── RewardAmount: long
│
│  Methods (client-side):
├── IsAdAvailable() → bool         → checks max views + cooldown
└── GetRemainingCooldown() → TimeSpan
```

#### `AdData : IGameModuleData`
Full client payload. Key: `"AdData"`.

```
AdData
└── Placements: Dictionary<string, AdPlacementClientData>
    └── Built from AdsPersistence × AdsConfig on the server
```

### Request / Response

```
WatchAdRequest : ModuleRequest<WatchAdResponse>
└── PlacementId: string

WatchAdResponse : ModuleResponse
├── Data: AdData           → updated placement states (always returned)
└── Responses[]            → nested responses when handlers enqueue them via GameApi
```

## Backend Logic (AdsService)

### `Initialize` — Called on `GameDataRequest`
```
Load AdsPersistence from PlayerData
Load AdsConfig from RemoteConfig
Return new AdData(persistence, config)   → client gets full placement map
```

### `WatchAd` — Called on `WatchAdRequest`

```
INPUT: WatchAdRequest { PlacementId }

1. Load AdsConfig + AdsPersistence
2. Resolve placementId (fallback to "default")
3. Get AdPlacementConfig for this placement

4. VALIDATE:
   IF HasReachedMaxViews → log warning, skip reward
   ELSE IF IsCooldownElapsed → ✓ valid
     a. RecordAdWatched(placementId)        → WatchCount++, LastWatched = now
     b. Player.Set(context, persistence) (write-through outside GameApi batch; deferred flush inside batch)
     c. GrantReward(placementConfig)         → dispatch to correct module
   ELSE → log warning (still on cooldown), skip reward

5. Build fresh AdData(persistence, config)
6. Return WatchAdResponse(adData) via ResolveResponse
   → merges any nested responses when a handler invokes other modules via `GameApiSession`

OUTPUT: WatchAdResponse { Data: AdData, Responses: [...] }
```

### `GrantReward` — Template behavior

```
IF RewardAmount <= 0 OR RewardType is empty → no reward, return

ELSE → log warning: configure a game-specific module / IGameSetup to grant rewards for this RewardType
```

## RemoteConfig JSON Example

```json
{
  "_placements": {
    "Main_Menu": {
      "CooldownSeconds": 300.0,
      "MaxViews": 5,
      "RewardType": "",
      "RewardAmount": 0
    },
    "Level_End": {
      "CooldownSeconds": 60.0,
      "MaxViews": 10,
      "RewardType": "",
      "RewardAmount": 0
    },
    "Daily_Bonus": {
      "CooldownSeconds": 86400.0,
      "MaxViews": 1,
      "RewardType": "",
      "RewardAmount": 0
    }
  }
}
```

## File Map

```
LiveOps/Scaffold/Ads.DTO/
├── AdPlacementConfig.cs        → Remote config per placement
├── AdPlacementState.cs         → Player persistence per placement
├── AdPlacementClientData.cs    → Merged client payload per placement
├── AdsConfig.cs                → IGameModuleData wrapper (RemoteConfig)
├── AdsPersistence.cs           → IGameModuleData wrapper (PlayerData)
├── AdData.cs                   → IGameModuleData client payload builder
└── Request/
    ├── WatchAdRequest.cs       → Client → Server request
    └── WatchAdResponse.cs      → Server → Client response

LiveOps/Scaffold/Ads/
└── AdsService.cs               → IGameModule + IGameApiHandler (validation + reward hook)
```

## AI Agent Context
- Invariants:
  - `RewardType` / `RewardAmount` are opaque in the template; wire them to your own modules if you grant rewards from ads.
  - `AdsPersistence` is the single source of truth for watch state; never mutate outside `AdsService`.
- Allowed Dependencies:
  - `GameApiDispatcher` / `IGameApiHandler<WatchAdRequest, WatchAdResponse>` (routing)
  - `IPlayerData`, `IRemoteConfig` (data access)
- Forbidden Dependencies:
  - Client-side assemblies (`Scaffold.Ads`, `Scaffold.Ads.Levelplay`)
  - Direct file or HTTP access from Cloud Code
- Change Checklist:
  - Adding rewards → fork `AdsService.GrantReward` or add a parallel `IGameSetup` flow; keep DTO contracts stable for clients.
  - Changing DTO fields → update both `AdPlacementConfig` and `AdPlacementClientData` + the `AdData` constructor mapping.
  - Run `dotnet build` in `LiveOps/` after any change.
- Known Tricky Areas:
  - `ResponseStatusType.Success` is enum 0 (default). Responses are "success" unless explicitly set otherwise.
  - `_placements` is `[JsonProperty]` private — Newtonsoft deserializes it directly. Don't rename without migration.
  - `ComputeNextAdAvailableUtcIso` uses ISO 8601 "O" format — client must parse with `DateTimeStyles.AdjustToUniversal`.

## Changelog
- 2026-04-25: Removed legacy Gold/Level Cloud Code modules; template `AdsService` no longer dispatches gold; document game-specific reward extension.
- 2026-04-02: Initial per-placement architecture. Added `RewardType`/`RewardAmount` to config. Backend reward dispatch via `GoldModule`. Extracted all DTOs to individual files.
