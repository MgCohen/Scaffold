using System;
using UnityEngine;

namespace Scaffold.Ads
{
    public class InterstitialAdManager : IDisposable
    {
        private const float interstitialCooldownSeconds = 5;

        private IInterstitialAdService adService;
        private DateTime lastAdCompletionTime;
        private bool isInitialized;

        public event Action<bool, string> AdSuccessfullyCompleted;
        public event Action<bool> AdAvailable;

        public void Initialize(IInterstitialAdService interstitialAdService, AdConfigurationSO _)
        {
            if (isInitialized)
            {
                return;
            }

            adService = interstitialAdService;

            adService.AdAvailable += HandleAdAvailable;
            adService.AdSuccessfullyCompleted += HandleAdSuccessfullyCompleted;

            isInitialized = true;
        }

        public async void ShowInterstitial(string placementName = null)
        {
            bool canShow = await CanShowAd(placementName);
            if (canShow)
            {
                Debug.Log($"Showing Interstitial with placement: {placementName ?? "default"}");
                adService.ShowAd(placementName);
            }
            else
            {
                Debug.LogWarning($"Cannot show Interstitial for placement: {placementName ?? "default"}");
            }
        }

        public async Awaitable<bool> CanShowAd(string placementName = null)
        {
            if (!isInitialized || adService == null)
            {
                Debug.LogWarning("InterstitialAdManager not initialized");
                return false;
            }

            bool isCooldownExpired = HasCooldownExpired();
            bool isAdReady = await adService.CanShowAd(placementName);

            if (!isAdReady)
            {
                Debug.LogWarning("Interstitial Ad not ready");
            }

            return isAdReady && isCooldownExpired;
        }

        private void HandleAdAvailable(bool available)
        {
            AdAvailable?.Invoke(available);
        }

        private void HandleAdSuccessfullyCompleted(bool success, string placementName)
        {
            lastAdCompletionTime = DateTime.UtcNow;
            AdSuccessfullyCompleted?.Invoke(success, placementName);
        }

        private bool HasCooldownExpired()
        {
            float remaining = GetRemainingCooldownSeconds();
            if (remaining > 0f)
            {
                Debug.Log($"Interstitial still on cooldown for {remaining:F1} seconds");
                return false;
            }
            return true;
        }

        private float GetRemainingCooldownSeconds()
        {
            if (lastAdCompletionTime == default)
            {
                return 0f;
            }

            TimeSpan timeSinceLastAd = DateTime.UtcNow - lastAdCompletionTime;
            float remaining = interstitialCooldownSeconds - (float)timeSinceLastAd.TotalSeconds;
            return Math.Max(0f, remaining);
        }

        public void Dispose()
        {
            if (adService != null)
            {
                adService.AdAvailable -= HandleAdAvailable;
                adService.AdSuccessfullyCompleted -= HandleAdSuccessfullyCompleted;
            }
        }
    }
}
