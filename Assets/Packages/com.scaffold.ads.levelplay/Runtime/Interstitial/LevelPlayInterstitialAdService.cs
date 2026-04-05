using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Scaffold.Ads.Levelplay
{
    public class LevelPlayInterstitialAdService : IInterstitialAdService, IDisposable
    {
        private readonly LevelPlayAdConfigurationSO _configuration;
        private readonly Dictionary<string, LevelPlayInterstitialAd> _interstitialAds = new Dictionary<string, LevelPlayInterstitialAd>();
        private readonly Dictionary<string, int> _loadRetryCounts = new Dictionary<string, int>();

        private const int MaxRetryAttempts = 3;
        private const float RetryDelaySeconds = 5f;

        public event Action<bool> AdAvailable;
        public event Action<bool, string> AdSuccessfullyCompleted;

        public LevelPlayInterstitialAdService(LevelPlayAdConfigurationSO configuration)
        {
            _configuration = configuration;
        }

        public void Initialize()
        {
            List<InterstitialAdConfig> activePlacements = _configuration.GetInterstitialPlacements();
            if (activePlacements != null)
            {
                foreach (InterstitialAdConfig placement in activePlacements)
                {
                    string unitId = placement.adUnitId;
                    if (string.IsNullOrEmpty(unitId)) continue;

                    if (!_interstitialAds.ContainsKey(placement.placementKey))
                    {
                        LevelPlayInterstitialAd ad = new LevelPlayInterstitialAd(unitId);
                        string key = placement.placementKey;

                        ad.OnAdLoaded += (adInfo) => HandleAdLoadedSuccessfully(key, adInfo);
                        ad.OnAdLoadFailed += (error) => HandleAdLoadFailed(key, ad, error);
                        ad.OnAdDisplayed += (adInfo) => HandleAdDisplayed(key, adInfo);
                        ad.OnAdDisplayFailed += (adInfo, error) => HandleAdFailedToDisplay(key, adInfo, error);
                        ad.OnAdClosed += (adInfo) => HandleAdClosed(key, ad, adInfo);

                        _interstitialAds[key] = ad;
                        _loadRetryCounts[key] = 0;

                        ad.LoadAd();
                    }
                }
            }
        }

        public async Awaitable<bool> CanShowAd(string placementName = null)
        {
            await Task.Yield();

            if (_interstitialAds.Count == 0) return false;

            if (!string.IsNullOrEmpty(placementName) && _interstitialAds.TryGetValue(placementName, out LevelPlayInterstitialAd ad))
            {
                return ad.IsAdReady();
            }

            return _interstitialAds.Values.Any(x => x.IsAdReady());
        }

        public async void ShowAd(string placementName = null)
        {
            bool canShow = await CanShowAd(placementName);
            LevelPlayInterstitialAd targetAd = null;

            if (!string.IsNullOrEmpty(placementName))
            {
                _interstitialAds.TryGetValue(placementName, out targetAd);
            }
            else if (_interstitialAds.Count > 0)
            {
                targetAd = _interstitialAds.Values.First();
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
            Debug.Log($"Interstitial ad loaded for {placementName}: {adInfo.AdNetwork}");
        }

        private void HandleAdLoadFailed(string placementName, LevelPlayInterstitialAd ad, LevelPlayAdError error)
        {
            Debug.LogError($"Interstitial ad failed to load for {placementName}: {error.ErrorMessage}");

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

        private void HandleAdClosed(string placementName, LevelPlayInterstitialAd ad, LevelPlayAdInfo adInfo)
        {
            AdSuccessfullyCompleted?.Invoke(true, placementName);
            ad?.LoadAd();
        }

        public void Dispose()
        {
            _interstitialAds.Clear();
            _loadRetryCounts.Clear();
        }
    }
}
