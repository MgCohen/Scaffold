# Ads LevelPlay Implementation

## Overview
This package contains the concrete implementations of the `Game.Ads.Interfaces` defined in the `Ads/Runtime` layer, specifically tailored for **IronSource LevelPlay**.

It relies on the `Unity.Services.LevelPlay` API package.

---

## Practical Usage Guide

### Setting Up LevelPlay Configurations
Instead of hardcoding APIs keys inside monolithic scripts or prefabs, this implementation relies on Unity's built-in `ScriptableObjects` acting as factories.

1. In your project window, Right-Click -> **Create -> Ads -> LevelPlay Configuration**.
2. A new `LevelPlayAdConfigurationSO` asset will be created. Fill in your **App Keys** and **Ad Unit IDs** for both Android and iOS inside this file.
3. Assign this `ScriptableObject` to the `AdConfiguration` slot in your generic `AdRewardManagerClient` instance.

### Extending
Since the implementation derives from the base `AdConfigurationSO`, if you migrate to AppLovin or UnityAds in the future, you simply create a new `ScriptableObject` class for that SDK, and swap the asset in your manager's inspector without changing any game code!
