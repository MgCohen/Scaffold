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
        private readonly Dictionary<string, LevelPlayRewardedAd> _rewardedAds = new Dictionary<string, LevelPlayRewardedAd>();
        private readonly Dictionary<string, int> _loadRetryCounts = new Dictionary<string, int>();

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

                    if (!_rewardedAds.ContainsKey(placement.placementKey))
                    {
                        var ad = new LevelPlayRewardedAd(unitId);
                        string key = placement.placementKey;

                        ad.OnAdLoaded += (adInfo) => HandleAdLoadedSuccessfully(key, adInfo);
                        ad.OnAdLoadFailed += (error) => HandleAdLoadFailed(key, ad, error);
                        ad.OnAdDisplayed += (adInfo) => HandleAdDisplayed(key, adInfo);
                        ad.OnAdDisplayFailed += (adInfo, error) => HandleAdFailedToDisplay(key, adInfo, error);
                        ad.OnAdRewarded += (adInfo, reward) => ProcessAdReward(key, adInfo, reward);
                        ad.OnAdClosed += (adInfo) => HandleAdClosed(key, ad, adInfo);

                        _rewardedAds[key] = ad;
                        _loadRetryCounts[key] = 0;

                        ad.LoadAd();
                    }
                }
            }
        }

        public async Awaitable<bool> CanShowAd(string placementName = null)
        {
            await Task.Yield();

            if (_rewardedAds.Count == 0) return false;

            if (!string.IsNullOrEmpty(placementName) && _rewardedAds.TryGetValue(placementName, out var ad))
            {
                return ad.IsAdReady();
            }

            return _rewardedAds.Values.Any(x => x.IsAdReady());
        }

        public async void ShowAd(string placementName = null)
        {
            bool canShow = await CanShowAd(placementName);
            LevelPlayRewardedAd targetAd = null;

            if (!string.IsNullOrEmpty(placementName))
            {
                _rewardedAds.TryGetValue(placementName, out targetAd);
            }
            else if (_rewardedAds.Count > 0)
            {
                targetAd = _rewardedAds.Values.First();
            }

            if (canShow && targetAd != null)
            {
                if (string.IsNullOrEmpty(placementName)) targetAd.ShowAd();
                else targetAd.ShowAd(placementName);
            }
            else
            {
                Debug.LogWarning("Ad not ready or unable to show right now.");
                targetAd?.LoadAd();
            }
        }

        private void HandleAdLoadedSuccessfully(string placementName, LevelPlayAdInfo adInfo)
        {
            if (_loadRetryCounts.ContainsKey(placementName)) _loadRetryCounts[placementName] = 0;
            AdAvailable?.Invoke(true);
            Debug.Log($"Rewarded ad loaded for {placementName}: {adInfo.AdNetwork}");
        }

        private void HandleAdLoadFailed(string placementName, LevelPlayRewardedAd ad, LevelPlayAdError error)
        {
            Debug.LogError($"Rewarded ad failed to load for {placementName}: {error.ErrorMessage}");

            if (!_loadRetryCounts.ContainsKey(placementName)) return;

            _loadRetryCounts[placementName]++;
            if (_loadRetryCounts[placementName] >= MaxRetryAttempts)
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

        private void HandleAdDisplayed(string placementName, LevelPlayAdInfo adInfo)
        {
            Debug.Log($"Ad Displayed for {placementName}");
        }

        private void HandleAdFailedToDisplay(string placementName, LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogError($"Ad {adInfo.AdNetwork} failed to display for {placementName}: {error}");
            AdSuccessfullyCompleted?.Invoke(false, placementName);
        }

        private void HandleAdClosed(string placementName, LevelPlayRewardedAd ad, LevelPlayAdInfo adInfo)
        {
            ad?.LoadAd();
        }

        private void ProcessAdReward(string placementName, LevelPlayAdInfo adInfo, LevelPlayReward reward)
        {
            Debug.Log($"Processing Ad Reward for {placementName}: {reward.Name} x{reward.Amount}");
            string token = $"{DateTime.UtcNow.Ticks}_{adInfo.InstanceId}_{adInfo.AdNetwork}_{reward.Name}_{reward.Amount}";
            AdSuccessfullyCompletedWithToken?.Invoke(true, placementName, token);
            AdSuccessfullyCompleted?.Invoke(true, placementName);
        }

        public void Dispose()
        {
            _rewardedAds.Clear();
            _loadRetryCounts.Clear();
        }
    }
}
