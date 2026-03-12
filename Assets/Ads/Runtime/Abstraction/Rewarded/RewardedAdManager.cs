using System;
using UnityEngine;
using Game.Ads.Interfaces;
using Game.Ads.Configurations;
using Game.Ads.Endpoint;

namespace Game.Ads
{
    /// <summary>
    /// Governs rewarded video ad pacing, events, and endpoint reward validation.
    /// Operates entirely via the IRewardedAdService abstraction.
    /// </summary>
    public class RewardedAdManager : MonoBehaviour
    {
        private AdConfigurationSO _adConfiguration;

        // Cooldown between rewarded ad completions
        private const float k_RewardCooldownSeconds = 10;
        private const string k_DefaultPlacementKey = "default";

        // Dependencies
        private IRewardedAdService _adService;
        private IRewardEndpointClient _rewardEndpointClient;

        private string _unityUserId;
        private string _lastAdToken;
        private System.Collections.Generic.Dictionary<string, DateTime> _lastAdCompletionTimes = new(StringComparer.OrdinalIgnoreCase);

        public event Action<bool, string> AdSuccessfullyCompleted;
        public event Action<bool> AdAvailable;

        private bool _isInitialized;

        public void Initialize(IRewardedAdService rewardedAdService, AdConfigurationSO config, string userId)
        {
            if (_isInitialized)
            {
                return;
            }

            _adService = rewardedAdService;
            _adConfiguration = config;
            _unityUserId = userId;

            _adService.AdAvailable += HandleAdAvailable;
            _adService.AdSuccessfullyCompletedWithToken += HandleAdSuccessfullyCompletedWithToken;
            _adService.AdSuccessfullyCompleted += HandleAdSuccessfullyCompleted;

            _isInitialized = true;
        }

        public void SetRewardEndpointClient(IRewardEndpointClient endpointClient)
        {
            _rewardEndpointClient = endpointClient;
        }

        #region Public Interface

        public async void ClickShowAdReward(string placementName = null)
        {
            string key = string.IsNullOrEmpty(placementName) ? k_DefaultPlacementKey : placementName;
            bool canShow = await CanShowAd(key);
            if (canShow)
            {
                Debug.Log($"Showing ad with placement: '{key}'");
                _adService.ShowAd(key);
            }
            else
            {
                Debug.LogWarning($"Cannot show ad for placement: {key}");
            }
        }

        public async Awaitable<bool> CanShowAd(string placementName = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("RewardedAdManager not initialized");
                return false;
            }

            if (_adService == null)
            {
                Debug.LogWarning("_adService == null");
                return false;
            }

            bool isCooldownExpired = HasCooldownExpired(placementName);
            bool isAdReady = await _adService.CanShowAd(placementName);

            if (!isAdReady)
            {
                Debug.LogWarning($"Ad not ready for placement '{placementName ?? "default"}' - still loading or no inventory available");
            }

            return isAdReady && isCooldownExpired;
        }

        #endregion

        #region Event Handlers

        private void HandleAdAvailable(bool available)
        {
            AdAvailable?.Invoke(available);
        }

        private async void HandleAdSuccessfullyCompletedWithToken(bool success, string placementName, string token)
        {
            if (!success) return;

            _lastAdToken = token;
            RecordCompletionTime(placementName);

            if (_rewardEndpointClient == null)
            {
                Debug.LogError("No IRewardEndpointClient provided to RewardedAdManager. Cannot grant reward.");
                AdSuccessfullyCompleted?.Invoke(false, placementName);
                return;
            }

            if (string.IsNullOrEmpty(_unityUserId))
            {
                Debug.LogError("No UnityUserId provided to RewardedAdManager. Cannot grant reward.");
                AdSuccessfullyCompleted?.Invoke(false, placementName);
                return;
            }

            if (!_adConfiguration.GetRewardedPlacement(placementName, out RewardedAdConfig placement) || string.IsNullOrEmpty(placement.rewardEndpointUrl))
            {
                Debug.LogWarning($"No endpoint configured for placement: {placementName}. Cannot grant reward.");
                AdSuccessfullyCompleted?.Invoke(false, placementName);
                return;
            }

            string endpointUrl = placement.rewardEndpointUrl;
            bool endpointSuccess = await _rewardEndpointClient.CallRewardEndpointAsync(_unityUserId, token, endpointUrl);

            if (endpointSuccess)
            {
                Debug.Log($"Endpoint granted reward successfully for {placementName}.");
                AdSuccessfullyCompleted?.Invoke(true, placementName);
            }
            else
            {
                Debug.LogWarning($"Failed to grant reward via endpoint for {placementName}.");
                AdSuccessfullyCompleted?.Invoke(false, placementName);
            }
        }

        private void HandleAdSuccessfullyCompleted(bool success, string placementName)
        {
            if (success)
            {
                RecordCompletionTime(placementName);
            }
            AdSuccessfullyCompleted?.Invoke(success, placementName);
        }

        #endregion

        #region Helper Methods

        public float GetRemainingCooldownSeconds(string placementName = null)
        {
            string key = string.IsNullOrEmpty(placementName) ? k_DefaultPlacementKey : placementName;
            if (!_lastAdCompletionTimes.TryGetValue(key, out DateTime lastTime)) return 0f;

            TimeSpan timeSinceLastAd = DateTime.UtcNow - lastTime;
            float remaining = k_RewardCooldownSeconds - (float)timeSinceLastAd.TotalSeconds;
            return Math.Max(0f, remaining);
        }

        private void RecordCompletionTime(string placementName)
        {
            string key = string.IsNullOrEmpty(placementName) ? k_DefaultPlacementKey : placementName;
            _lastAdCompletionTimes[key] = DateTime.UtcNow;
            Debug.Log($"[RewardedAdManager] Recorded completion for '{key}' at {DateTime.UtcNow}");
        }

        private bool HasCooldownExpired(string placementName)
        {
            string key = string.IsNullOrEmpty(placementName) ? k_DefaultPlacementKey : placementName;
            float remaining = GetRemainingCooldownSeconds(key);
            if (remaining > 0f)
            {
                Debug.Log($"Ad for placement '{key}' still on cooldown for {remaining:F1} seconds");
                return false;
            }
            return true;
        }

        #endregion

        private void OnDestroy()
        {
            if (_adService != null)
            {
                _adService.AdAvailable -= HandleAdAvailable;
                _adService.AdSuccessfullyCompletedWithToken -= HandleAdSuccessfullyCompletedWithToken;
                _adService.AdSuccessfullyCompleted -= HandleAdSuccessfullyCompleted;
            }
        }
    }
}