using System;
using UnityEngine;

namespace Scaffold.Ads
{
    public class RewardedAdManager : IDisposable
    {
        private const float rewardCooldownSeconds = 10;
        private const string defaultPlacementKey = "default";

        private IRewardedAdService adService;
        private IRewardEndpointClient rewardEndpointClient;
        private string unityUserId;
        private string lastAdToken;
        private System.Collections.Generic.Dictionary<string, DateTime> lastAdCompletionTimes = new(StringComparer.OrdinalIgnoreCase);
        private bool isInitialized;

        public event Action<bool, string> AdSuccessfullyCompleted;
        public event Action<bool> AdAvailable;

        public void Initialize(IRewardedAdService rewardedAdService, AdConfigurationSO _, string userId)
        {
            if (isInitialized)
            {
                return;
            }

            adService = rewardedAdService;
            unityUserId = userId;

            adService.AdAvailable += HandleAdAvailable;
            adService.AdSuccessfullyCompletedWithToken += HandleAdSuccessfullyCompletedWithToken;
            adService.AdSuccessfullyCompleted += HandleAdSuccessfullyCompleted;

            isInitialized = true;
        }

        public void SetRewardEndpointClient(IRewardEndpointClient endpointClient)
        {
            rewardEndpointClient = endpointClient;
        }

        public async void ClickShowAdReward(string placementName = null)
        {
            string key = string.IsNullOrEmpty(placementName) ? defaultPlacementKey : placementName;
            bool canShow = await CanShowAd(key);
            if (canShow)
            {
                Debug.Log($"Showing ad with placement: '{key}'");
                adService.ShowAd(key);
            }
            else
            {
                Debug.LogWarning($"Cannot show ad for placement: {key}");
            }
        }

        public async Awaitable<bool> CanShowAd(string placementName = null)
        {
            if (!TryValidateAdServiceReady())
            {
                return false;
            }

            bool isCooldownExpired = HasCooldownExpired(placementName);
            bool isAdReady = await adService.CanShowAd(placementName);

            if (!isAdReady)
            {
                Debug.LogWarning($"Ad not ready for placement '{placementName ?? "default"}' - still loading or no inventory available");
            }

            return isAdReady && isCooldownExpired;
        }

        private bool TryValidateAdServiceReady()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("RewardedAdManager not initialized");
                return false;
            }

            if (adService == null)
            {
                Debug.LogWarning("adService == null");
                return false;
            }

            return true;
        }

        private void HandleAdAvailable(bool available)
        {
            AdAvailable?.Invoke(available);
        }

        private async void HandleAdSuccessfullyCompletedWithToken(bool success, string placementName, string token)
        {
            if (!success)
            {
                return;
            }

            lastAdToken = token;
            RecordCompletionTime(placementName);

            if (!await TryCompleteRewardEndpoint(placementName))
            {
                return;
            }
        }

        private async System.Threading.Tasks.Task<bool> TryCompleteRewardEndpoint(string placementName)
        {
            if (!ValidateRewardEndpointClaims(placementName))
            {
                return false;
            }

            bool endpointSuccess = await rewardEndpointClient.CallRewardEndpointAsync(unityUserId, placementName, lastAdToken);
            NotifyRewardEndpointOutcome(placementName, endpointSuccess);
            return true;
        }

        private bool ValidateRewardEndpointClaims(string placementName)
        {
            if (rewardEndpointClient == null)
            {
                Debug.LogError("No IRewardEndpointClient provided to RewardedAdManager. Cannot grant reward.");
                AdSuccessfullyCompleted?.Invoke(false, placementName);
                return false;
            }

            if (string.IsNullOrEmpty(unityUserId))
            {
                Debug.LogError("No UnityUserId provided to RewardedAdManager. Cannot grant reward.");
                AdSuccessfullyCompleted?.Invoke(false, placementName);
                return false;
            }

            return true;
        }

        private void NotifyRewardEndpointOutcome(string placementName, bool endpointSuccess)
        {
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

        private bool HasCooldownExpired(string placementName)
        {
            string key = string.IsNullOrEmpty(placementName) ? defaultPlacementKey : placementName;
            float remaining = GetRemainingCooldownSeconds(key);
            if (remaining > 0f)
            {
                Debug.Log($"Ad for placement '{key}' still on cooldown for {remaining:F1} seconds");
                return false;
            }
            return true;
        }

        public float GetRemainingCooldownSeconds(string placementName = null)
        {
            string key = string.IsNullOrEmpty(placementName) ? defaultPlacementKey : placementName;
            if (!lastAdCompletionTimes.TryGetValue(key, out DateTime lastTime))
            {
                return 0f;
            }

            TimeSpan timeSinceLastAd = DateTime.UtcNow - lastTime;
            float remaining = rewardCooldownSeconds - (float)timeSinceLastAd.TotalSeconds;
            return Math.Max(0f, remaining);
        }

        private void RecordCompletionTime(string placementName)
        {
            string key = string.IsNullOrEmpty(placementName) ? defaultPlacementKey : placementName;
            lastAdCompletionTimes[key] = DateTime.UtcNow;
            Debug.Log($"[RewardedAdManager] Recorded completion for '{key}' at {DateTime.UtcNow}");
        }

        public void Dispose()
        {
            if (adService != null)
            {
                adService.AdAvailable -= HandleAdAvailable;
                adService.AdSuccessfullyCompletedWithToken -= HandleAdSuccessfullyCompletedWithToken;
                adService.AdSuccessfullyCompleted -= HandleAdSuccessfullyCompleted;
            }
        }
    }
}
