using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Scaffold.Ads.Levelplay
{
    public class LevelPlayRewardedAdService : IRewardedAdService, IDisposable
    {
        private const int maxRetryAttempts = 3;
        private const float retryDelaySeconds = 5f;

        public LevelPlayRewardedAdService(LevelPlayAdConfigurationSO configuration)
        {
            this.configuration = configuration;
        }

        private readonly LevelPlayAdConfigurationSO configuration;
        private readonly Dictionary<string, LevelPlayRewardedAd> adsByUnitId = new Dictionary<string, LevelPlayRewardedAd>();
        private readonly Dictionary<string, string> placementToUnitId = new Dictionary<string, string>();
        private readonly Dictionary<string, int> loadRetryCounts = new Dictionary<string, int>();
        private string currentShowingPlacement;

        public event Action<bool> AdAvailable;
        public event Action<bool, string, string> AdSuccessfullyCompletedWithToken;
        public event Action<bool, string> AdSuccessfullyCompleted;

        public void Initialize()
        {
            List<RewardedAdConfig> activePlacements = configuration.GetRewardedPlacements();
            if (activePlacements == null)
            {
                return;
            }

            foreach (RewardedAdConfig placement in activePlacements)
            {
                RegisterRewardedPlacement(placement);
            }
        }

        private void RegisterRewardedPlacement(RewardedAdConfig placement)
        {
            string unitId = placement.AdUnitId;
            if (string.IsNullOrEmpty(unitId))
            {
                return;
            }

            placementToUnitId[placement.PlacementKey] = unitId;

            if (adsByUnitId.ContainsKey(unitId))
            {
                return;
            }

            LevelPlayRewardedAd ad = new LevelPlayRewardedAd(unitId);
            WireRewardedAdEvents(ad, unitId);
            adsByUnitId[unitId] = ad;
            loadRetryCounts[unitId] = 0;
            ad.LoadAd();
        }

        private void WireRewardedAdEvents(LevelPlayRewardedAd ad, string unitId)
        {
            ad.OnAdLoaded += (adInfo) => HandleAdLoadedSuccessfully(unitId, adInfo);
            ad.OnAdLoadFailed += (error) => HandleAdLoadFailed(unitId, ad, error);
            ad.OnAdDisplayed += (adInfo) => HandleAdDisplayed(adInfo);
            ad.OnAdDisplayFailed += (adInfo, error) => HandleAdFailedToDisplay(adInfo, error);
            ad.OnAdRewarded += (adInfo, reward) => ProcessAdReward(adInfo, reward);
            ad.OnAdClosed += (adInfo) => HandleAdClosed(unitId, ad, adInfo);
        }

        public async void ShowAd(string placementName = null)
        {
            bool canShow = await CanShowAd(placementName);
            LevelPlayRewardedAd targetAd = ResolveTargetAd(placementName);
            if (!TryShowRewarded(placementName, canShow, targetAd))
            {
                Debug.LogWarning("Ad not ready or unable to show right now.");
                targetAd?.LoadAd();
            }
        }

        private bool TryShowRewarded(string placementName, bool canShow, LevelPlayRewardedAd targetAd)
        {
            if (!canShow || targetAd == null)
            {
                return false;
            }

            currentShowingPlacement = string.IsNullOrEmpty(placementName) ? "default" : placementName;
            if (string.IsNullOrEmpty(placementName))
            {
                targetAd.ShowAd();
            }
            else
            {
                targetAd.ShowAd(placementName);
            }
            return true;
        }

        public async Awaitable<bool> CanShowAd(string placementName = null)
        {
            await Task.Yield();

            if (adsByUnitId.Count == 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(placementName) && placementToUnitId.TryGetValue(placementName, out string unitId))
            {
                if (adsByUnitId.TryGetValue(unitId, out LevelPlayRewardedAd ad))
                {
                    return ad.IsAdReady();
                }
            }

            return adsByUnitId.Values.Any(x => x.IsAdReady());
        }

        private LevelPlayRewardedAd ResolveTargetAd(string placementName)
        {
            if (!string.IsNullOrEmpty(placementName) && placementToUnitId.TryGetValue(placementName, out string unitId))
            {
                if (adsByUnitId.TryGetValue(unitId, out LevelPlayRewardedAd ad))
                {
                    return ad;
                }
                return null;
            }

            if (adsByUnitId.Count > 0)
            {
                return adsByUnitId.Values.First();
            }

            return null;
        }

        private void HandleAdLoadedSuccessfully(string unitId, LevelPlayAdInfo adInfo)
        {
            if (loadRetryCounts.ContainsKey(unitId))
            {
                loadRetryCounts[unitId] = 0;
            }
            AdAvailable?.Invoke(true);
            Debug.Log($"Rewarded ad loaded for unitId {unitId}: {adInfo.AdNetwork}");
        }

        private void HandleAdLoadFailed(string unitId, LevelPlayRewardedAd ad, LevelPlayAdError error)
        {
            LogRewardedLoadFailure(unitId, error);

            if (!loadRetryCounts.ContainsKey(unitId))
            {
                return;
            }

            loadRetryCounts[unitId]++;
            if (loadRetryCounts[unitId] >= maxRetryAttempts)
            {
                AdAvailable?.Invoke(false);
                return;
            }

            ScheduleRewardedReload(ad);
        }

        private void LogRewardedLoadFailure(string unitId, LevelPlayAdError error)
        {
            string errorReason = error.ErrorMessage;
            if (errorReason.Contains("No fill", StringComparison.OrdinalIgnoreCase))
            {
                errorReason += " (This is normal during development or if networks have no inventory)";
            }
            Debug.LogWarning($"Rewarded ad failed to load for unitId {unitId}: {errorReason}");
        }

        private void ScheduleRewardedReload(LevelPlayRewardedAd ad)
        {
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                ad?.LoadAd();
            });
        }

        private void HandleAdDisplayed(LevelPlayAdInfo adInfo)
        {
            string placementName = currentShowingPlacement ?? "default";
            Debug.Log($"Ad Displayed for {placementName}");
        }

        private void HandleAdFailedToDisplay(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            string placementName = currentShowingPlacement ?? "default";
            Debug.LogError($"Ad {adInfo.AdNetwork} failed to display for {placementName}: {error}");
            AdSuccessfullyCompleted?.Invoke(false, placementName);
            currentShowingPlacement = null;
        }

        private void HandleAdClosed(string unitId, LevelPlayRewardedAd ad, LevelPlayAdInfo adInfo)
        {
            ad?.LoadAd();
            currentShowingPlacement = null;
        }

        private void ProcessAdReward(LevelPlayAdInfo adInfo, LevelPlayReward reward)
        {
            string placementName = currentShowingPlacement ?? "default";
            Debug.Log($"Processing Ad Reward for {placementName}: {reward.Name} x{reward.Amount}");
            string token = $"{DateTime.UtcNow.Ticks}_{adInfo.InstanceId}_{adInfo.AdNetwork}_{reward.Name}_{reward.Amount}";
            AdSuccessfullyCompletedWithToken?.Invoke(true, placementName, token);
            AdSuccessfullyCompleted?.Invoke(true, placementName);
        }

        public void Dispose()
        {
            adsByUnitId.Clear();
            placementToUnitId.Clear();
            loadRetryCounts.Clear();
        }
    }
}
