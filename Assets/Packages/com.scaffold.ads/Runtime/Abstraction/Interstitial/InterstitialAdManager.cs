using System;
using UnityEngine;

namespace Scaffold.Ads
{
    public class InterstitialAdManager : IDisposable
    {
        private AdConfigurationSO _adConfiguration;
        private IInterstitialAdService _adService;

        private const float k_InterstitialCooldownSeconds = 5;
        private DateTime _lastAdCompletionTime;

        public event Action<bool, string> AdSuccessfullyCompleted;
        public event Action<bool> AdAvailable;

        private bool _isInitialized;

        public void Initialize(IInterstitialAdService interstitialAdService, AdConfigurationSO config)
        {
            if (_isInitialized) return;

            _adService = interstitialAdService;
            _adConfiguration = config;

            _adService.AdAvailable += HandleAdAvailable;
            _adService.AdSuccessfullyCompleted += HandleAdSuccessfullyCompleted;

            _isInitialized = true;
        }

        public async void ShowInterstitial(string placementName = null)
        {
            bool canShow = await CanShowAd(placementName);
            if (canShow)
            {
                Debug.Log($"Showing Interstitial with placement: {placementName ?? "default"}");
                _adService.ShowAd(placementName);
            }
            else
            {
                Debug.LogWarning($"Cannot show Interstitial for placement: {placementName ?? "default"}");
            }
        }

        public async Awaitable<bool> CanShowAd(string placementName = null)
        {
            if (!_isInitialized || _adService == null)
            {
                Debug.LogWarning("InterstitialAdManager not initialized");
                return false;
            }

            bool isCooldownExpired = HasCooldownExpired();
            bool isAdReady = await _adService.CanShowAd(placementName);

            if (!isAdReady) Debug.LogWarning("Interstitial Ad not ready");

            return isAdReady && isCooldownExpired;
        }

        private void HandleAdAvailable(bool available)
        {
            AdAvailable?.Invoke(available);
        }

        private void HandleAdSuccessfullyCompleted(bool success, string placementName)
        {
            _lastAdCompletionTime = DateTime.UtcNow;
            AdSuccessfullyCompleted?.Invoke(success, placementName);
        }

        private float GetRemainingCooldownSeconds()
        {
            if (_lastAdCompletionTime == default) return 0f;

            TimeSpan timeSinceLastAd = DateTime.UtcNow - _lastAdCompletionTime;
            float remaining = k_InterstitialCooldownSeconds - (float)timeSinceLastAd.TotalSeconds;
            return Math.Max(0f, remaining);
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

        public void Dispose()
        {
            if (_adService != null)
            {
                _adService.AdAvailable -= HandleAdAvailable;
                _adService.AdSuccessfullyCompleted -= HandleAdSuccessfullyCompleted;
            }
        }
    }
}
