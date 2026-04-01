# Ads LevelPlay Implementation - Walkthrough

This document outlines the implementation details and integration decisions of the IronSource LevelPlay bridging layer.

### 1. Concrete SDK Implementation
- `LevelPlayAdService.cs` implements the abstracted `IAdService` interface for LevelPlay.
- Stripped legacy mediation format `com.unity3d.mediation.LevelPlayAdFormat` overrides, transitioning fully to standard `Unity.Services.LevelPlay` SDK signatures.
- Implemented robust `HandleAdLoadFailed` internal retry delay task handling before notifying the abstraction layers.

### 2. Configuration Injection
- Created `LevelPlayAdConfigurationSO.cs` holding iOS and Android platform-specific `AppKey` and `RewardedAdUnitId`s properties.
- This creates the `LevelPlayAdService` dynamically, abstracting the factory pattern out of the monolithic managers.

### 3. Signature Matching
- Resolved the SDK's `Action<LevelPlayAdInfo, LevelPlayAdError>` delegation requirements for standard IronSource Ad Display failure callbacks without compiling against obsolete Legacy namespace objects.
