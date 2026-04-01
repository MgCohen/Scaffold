# Ads Core Runtime - Walkthrough

This document outlines the implementation details and decisions of the `Ads/Runtime` abstraction module.

### 1. Agnostic Abstraction Layer
- Designed a core `IAdService.cs` interface to handle SDK initialization, availability checks, and event callbacks (`AdAvailable`, `AdSuccessfullyCompletedWithToken`).
- decoupled entirely from any specific SDK namespaces (like `com.unity3d.mediation`).

### 2. AdRewardManager Orchestration
- The `AdRewardManager` serves as the public API for the game.
- It enforces **Single Responsibility (SRP)** by ONLY caring about Ad Availability, Initialization via the `IAdService` interface, caching, and 3-second minimum cooldown pacing.
- **Integrated Reward Validation**: It now includes the bridging logic (previously in a separate Validator) to handle HTTP POSTs to the server with the `{ UnityUserId, token }` acquired from the ad completion.
- This keeps the "glue" logic contained within the manager while maintaining 100% decoupling from the specific Ad Provider (LevelPlay, UnityAds, etc.).
- It exposes simple injection methods (`SetRewardEndpointClient`, `SetUnityUserId`) so the Game App can provide context without the Ads system needing to know about Auth SDKs.

### 4. Memory Management & UI Decoupling
- `IAdService` now strongly enforces `IDisposable` to guarantee global events (like `LevelPlay.OnInitSuccess`) are purged when the Manager is destroyed, preventing memory leaks if the Ads scene restarts.
- `AdRewardUIController.cs` maps the `AdRewardManager` state directly to standard `UnityEngine.UI.Button` iteractability. It strictly binds to events inside `OnEnable` and `OnDisable` to avoid classic Unity lifecycle mismatch bugs. visual state automatically.

### 5. Raw Validation Endpoints
- Included a `RewardEndpointClient` inside `Runtime/Implementation` for a standard HTTP fallback, making raw `{ UnityUserId, rewardAdId }` JSON posts using `UnityWebRequest` instead of relying on auto-generated strongly typed SDK bindings.
