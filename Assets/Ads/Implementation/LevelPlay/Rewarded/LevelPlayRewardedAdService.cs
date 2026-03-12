using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Ads.Configurations;
using Game.Ads.Interfaces;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Game.Ads.Services
{
    public class LevelPlayRewardedAdService : IRewardedAdService, IDisposable
    {
        private readonly LevelPlayAdConfigurationSO _configuration;
        private readonly Dictionary<string, LevelPlayRewardedAd> _adsByUnitId = new Dictionary<string, LevelPlayRewardedAd>();
        private readonly Dictionary<string, string> _placementToUnitId = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _loadRetryCounts = new Dictionary<string, int>();

        private string _currentShowingPlacement;

        private const int MaxRetryAttempts = 3;
        private const float RetryDelaySeconds = 5f;

        public event Action<bool> AdAvailable;
        public event Action<bool, string, string> AdSuccessfullyCompletedWithToken;
        public event Action<bool, string> AdSuccessfullyCompleted;

        public LevelPlayRewardedAdService(LevelPlayAdConfigurationSO configuration)
        {
            _configuration = configuration;
        }

        public void Initialize()
        {
            var activePlacements = _configuration.GetRewardedPlacements();
            if (activePlacements != null)
            {
                foreach (var placement in activePlacements)
                {
                    string unitId = placement.adUnitId;
                    if (string.IsNullOrEmpty(unitId)) continue;

                    _placementToUnitId[placement.placementKey] = unitId;

                    if (!_adsByUnitId.ContainsKey(unitId))
                    {
                        var ad = new LevelPlayRewardedAd(unitId);

                        ad.OnAdLoaded += (adInfo) => HandleAdLoadedSuccessfully(unitId, adInfo);
                        ad.OnAdLoadFailed += (error) => HandleAdLoadFailed(unitId, ad, error);
                        ad.OnAdDisplayed += (adInfo) => HandleAdDisplayed(adInfo);
                        ad.OnAdDisplayFailed += (adInfo, error) => HandleAdFailedToDisplay(adInfo, error);
                        ad.OnAdRewarded += (adInfo, reward) => ProcessAdReward(adInfo, reward);
                        ad.OnAdClosed += (adInfo) => HandleAdClosed(unitId, ad, adInfo);

                        _adsByUnitId[unitId] = ad;
                        _loadRetryCounts[unitId] = 0;

                        ad.LoadAd();
                    }
                }
            }
        }

        public async Awaitable<bool> CanShowAd(string placementName = null)
        {
            await Task.Yield();

            if (_adsByUnitId.Count == 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(placementName) && _placementToUnitId.TryGetValue(placementName, out var unitId))
            {
                if (_adsByUnitId.TryGetValue(unitId, out var ad))
                {
                    return ad.IsAdReady();
                }
            }

            return _adsByUnitId.Values.Any(x => x.IsAdReady());
        }

        public async void ShowAd(string placementName = null)
        {
            bool canShow = await CanShowAd(placementName);
            LevelPlayRewardedAd targetAd = null;

            if (!string.IsNullOrEmpty(placementName) && _placementToUnitId.TryGetValue(placementName, out var unitId))
            {
                _adsByUnitId.TryGetValue(unitId, out targetAd);
            }
            else if (_adsByUnitId.Count > 0)
            {
                targetAd = _adsByUnitId.Values.First();
            }

            if (canShow && targetAd != null)
            {
                _currentShowingPlacement = string.IsNullOrEmpty(placementName) ? "default" : placementName;
                if (string.IsNullOrEmpty(placementName)) targetAd.ShowAd();
                else targetAd.ShowAd(placementName);
            }
            else
            {
                Debug.LogWarning("Ad not ready or unable to show right now.");
                targetAd?.LoadAd();
            }
        }

        private void HandleAdLoadedSuccessfully(string unitId, LevelPlayAdInfo adInfo)
        {
            if (_loadRetryCounts.ContainsKey(unitId)) _loadRetryCounts[unitId] = 0;
            AdAvailable?.Invoke(true);
            Debug.Log($"Rewarded ad loaded for unitId {unitId}: {adInfo.AdNetwork}");
        }

        private void HandleAdLoadFailed(string unitId, LevelPlayRewardedAd ad, LevelPlayAdError error)
        {
            string errorReason = error.ErrorMessage;
            if (errorReason.Contains("No fill", StringComparison.OrdinalIgnoreCase))
            {
                errorReason += " (This is normal during development or if networks have no inventory)";
            }
            Debug.LogWarning($"Rewarded ad failed to load for unitId {unitId}: {errorReason}");

            if (!_loadRetryCounts.ContainsKey(unitId)) return;

            _loadRetryCounts[unitId]++;
            if (_loadRetryCounts[unitId] >= MaxRetryAttempts)
            {
                AdAvailable?.Invoke(false);
                return;
            }

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(RetryDelaySeconds));
                ad?.LoadAd();
            });
        }

        private void HandleAdDisplayed(LevelPlayAdInfo adInfo)
        {
            string placementName = _currentShowingPlacement ?? "default";
            Debug.Log($"Ad Displayed for {placementName}");
        }

        private void HandleAdFailedToDisplay(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            string placementName = _currentShowingPlacement ?? "default";
            Debug.LogError($"Ad {adInfo.AdNetwork} failed to display for {placementName}: {error}");
            AdSuccessfullyCompleted?.Invoke(false, placementName);
            _currentShowingPlacement = null;
        }

        private void HandleAdClosed(string unitId, LevelPlayRewardedAd ad, LevelPlayAdInfo adInfo)
        {
            ad?.LoadAd();
            _currentShowingPlacement = null;
        }

        private void ProcessAdReward(LevelPlayAdInfo adInfo, LevelPlayReward reward)
        {
            string placementName = _currentShowingPlacement ?? "default";
            Debug.Log($"Processing Ad Reward for {placementName}: {reward.Name} x{reward.Amount}");
            string token = $"{DateTime.UtcNow.Ticks}_{adInfo.InstanceId}_{adInfo.AdNetwork}_{reward.Name}_{reward.Amount}";
            AdSuccessfullyCompletedWithToken?.Invoke(true, placementName, token);
            AdSuccessfullyCompleted?.Invoke(true, placementName);
        }

        public void Dispose()
        {
            _adsByUnitId.Clear();
            _placementToUnitId.Clear();
            _loadRetryCounts.Clear();
        }
    }
}
