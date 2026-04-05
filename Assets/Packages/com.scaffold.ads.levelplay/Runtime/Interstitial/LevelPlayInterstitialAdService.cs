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
        private const int maxRetryAttempts = 3;
        private const float retryDelaySeconds = 5f;

        public LevelPlayInterstitialAdService(LevelPlayAdConfigurationSO configuration)
        {
            this.configuration = configuration;
        }

        private readonly LevelPlayAdConfigurationSO configuration;
        private readonly Dictionary<string, LevelPlayInterstitialAd> interstitialAds = new Dictionary<string, LevelPlayInterstitialAd>();
        private readonly Dictionary<string, int> loadRetryCounts = new Dictionary<string, int>();

        public event Action<bool> AdAvailable;
        public event Action<bool, string> AdSuccessfullyCompleted;

        public void Initialize()
        {
            List<InterstitialAdConfig> activePlacements = configuration.GetInterstitialPlacements();
            if (activePlacements == null)
            {
                return;
            }

            foreach (InterstitialAdConfig placement in activePlacements)
            {
                RegisterInterstitialPlacement(placement);
            }
        }

        private void RegisterInterstitialPlacement(InterstitialAdConfig placement)
        {
            string unitId = placement.AdUnitId;
            if (string.IsNullOrEmpty(unitId))
            {
                return;
            }

            string key = placement.PlacementKey;
            if (interstitialAds.ContainsKey(key))
            {
                return;
            }

            LevelPlayInterstitialAd ad = new LevelPlayInterstitialAd(unitId);
            WireInterstitialAdEvents(ad, key);
            interstitialAds[key] = ad;
            loadRetryCounts[key] = 0;
            ad.LoadAd();
        }

        private void WireInterstitialAdEvents(LevelPlayInterstitialAd ad, string key)
        {
            ad.OnAdLoaded += (adInfo) => HandleAdLoadedSuccessfully(key, adInfo);
            ad.OnAdLoadFailed += (error) => HandleAdLoadFailed(key, ad, error);
            ad.OnAdDisplayed += (adInfo) => HandleAdDisplayed(key, adInfo);
            ad.OnAdDisplayFailed += (adInfo, error) => HandleAdFailedToDisplay(key, adInfo, error);
            ad.OnAdClosed += (adInfo) => HandleAdClosed(key, ad, adInfo);
        }

        public async void ShowAd(string placementName = null)
        {
            bool canShow = await CanShowAd(placementName);
            LevelPlayInterstitialAd targetAd = ResolveTargetAd(placementName);
            if (!TryShowInterstitial(placementName, canShow, targetAd))
            {
                Debug.LogWarning("Ad not ready or unable to show right now.");
                targetAd?.LoadAd();
            }
        }

        private bool TryShowInterstitial(string placementName, bool canShow, LevelPlayInterstitialAd targetAd)
        {
            if (!canShow || targetAd == null)
            {
                return false;
            }

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

            if (interstitialAds.Count == 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(placementName) && interstitialAds.TryGetValue(placementName, out LevelPlayInterstitialAd ad))
            {
                return ad.IsAdReady();
            }

            return interstitialAds.Values.Any(x => x.IsAdReady());
        }

        private LevelPlayInterstitialAd ResolveTargetAd(string placementName)
        {
            if (!string.IsNullOrEmpty(placementName) && interstitialAds.TryGetValue(placementName, out LevelPlayInterstitialAd named))
            {
                return named;
            }

            if (interstitialAds.Count > 0)
            {
                return interstitialAds.Values.First();
            }

            return null;
        }

        private void HandleAdLoadedSuccessfully(string placementName, LevelPlayAdInfo adInfo)
        {
            if (loadRetryCounts.ContainsKey(placementName))
            {
                loadRetryCounts[placementName] = 0;
            }
            AdAvailable?.Invoke(true);
            Debug.Log($"Interstitial ad loaded for {placementName}: {adInfo.AdNetwork}");
        }

        private void HandleAdLoadFailed(string placementName, LevelPlayInterstitialAd ad, LevelPlayAdError error)
        {
            Debug.LogError($"Interstitial ad failed to load for {placementName}: {error.ErrorMessage}");

            if (!loadRetryCounts.ContainsKey(placementName))
            {
                return;
            }

            loadRetryCounts[placementName]++;
            if (loadRetryCounts[placementName] >= maxRetryAttempts)
            {
                AdAvailable?.Invoke(false);
                return;
            }

            ScheduleReloadAdInterstitial(ad);
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
            interstitialAds.Clear();
            loadRetryCounts.Clear();
        }

        private void ScheduleReloadAdInterstitial(LevelPlayInterstitialAd ad)
        {
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                ad?.LoadAd();
            });
        }
    }
}
