using System;
using System.Collections.Generic;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Scaffold.Ads.Levelplay
{
    public class LevelPlayBannerAdService : IBannerAdService, IDisposable
    {
        private readonly LevelPlayAdConfigurationSO _configuration;
        private readonly Dictionary<string, LevelPlayBannerAd> _bannerAds = new Dictionary<string, LevelPlayBannerAd>();

        public event Action<bool> BannerLoaded;

        public LevelPlayBannerAdService(LevelPlayAdConfigurationSO configuration)
        {
            _configuration = configuration;
        }

        public void Initialize()
        {
            var activePlacements = _configuration.GetBannerPlacements();
            if (activePlacements != null)
            {
                foreach (var placement in activePlacements)
                {
                    string unitId = placement.adUnitId;
                    if (string.IsNullOrEmpty(unitId)) continue;

                    if (!_bannerAds.ContainsKey(placement.placementKey))
                    {
                        LevelPlayBannerPosition position = GetLevelPlayPosition(placement.bannerPosition);
                        LevelPlayBannerAd.Config config = new LevelPlayBannerAd.Config.Builder()
                            .SetPosition(position)
                            .Build();
                        var ad = new LevelPlayBannerAd(unitId, config);
                        string key = placement.placementKey;

                        ad.OnAdLoaded += (adInfo) => HandleAdLoadedSuccessfully(key, adInfo);
                        ad.OnAdLoadFailed += (error) => HandleAdLoadFailed(key, error);
                        ad.OnAdDisplayed += (adInfo) => HandleAdDisplayed(key, adInfo);
                        ad.OnAdDisplayFailed += (adInfo, error) => HandleAdFailedToDisplay(key, adInfo, error);
                        ad.OnAdCollapsed += (adInfo) => HandleAdCollapsed(key, adInfo);
                        ad.OnAdExpanded += (adInfo) => HandleAdExpanded(key, adInfo);

                        _bannerAds[key] = ad;
                    }
                }
            }
        }

        private LevelPlayBannerPosition GetLevelPlayPosition(BannerPosition pos)
        {
            switch (pos)
            {
                case BannerPosition.Top: return LevelPlayBannerPosition.TopCenter;
                case BannerPosition.Bottom: return LevelPlayBannerPosition.BottomCenter;
                case BannerPosition.Center: return LevelPlayBannerPosition.Center;
                default: return LevelPlayBannerPosition.BottomCenter;
            }
        }

        public void LoadBanner(string placementName = null)
        {
            var targetAd = GetTargetAd(placementName);
            if (targetAd != null)
            {
                targetAd.LoadAd();
            }
        }

        public void ShowBanner(string placementName = null, BannerPosition? position = null)
        {
            // LevelPlay Banner ads show automatically when loaded if not hidden, 
            // but we can call ShowAd explicitly to un-hide if hidden previously.
            var targetAd = GetTargetAd(placementName);
            if (targetAd != null)
            {
                if (position.HasValue)
                {
                    Debug.LogWarning("LevelPlay SDK does not natively support changing banner position after creation. Ignoring position override.");
                }
                targetAd.ShowAd();
            }
        }

        public void HideBanner(string placementName = null)
        {
            var targetAd = GetTargetAd(placementName);
            targetAd?.HideAd();
        }

        public void DestroyBanner(string placementName = null)
        {
            var targetAd = GetTargetAd(placementName);
            if (targetAd != null)
            {
                targetAd.DestroyAd();
                // Optionally remove from dictionary or keep for reloading later.
            }
        }

        private LevelPlayBannerAd GetTargetAd(string placementName)
        {
            if (string.IsNullOrEmpty(placementName))
            {
                if (_bannerAds.Count > 0)
                {
                    var enumerator = _bannerAds.Values.GetEnumerator();
                    enumerator.MoveNext();
                    return enumerator.Current;
                }
                return null;
            }

            _bannerAds.TryGetValue(placementName, out var targetAd);
            return targetAd;
        }

        private void HandleAdLoadedSuccessfully(string placementName, LevelPlayAdInfo adInfo)
        {
            BannerLoaded?.Invoke(true);
            Debug.Log($"Banner loaded for {placementName}: {adInfo.AdNetwork}");
        }

        private void HandleAdLoadFailed(string placementName, LevelPlayAdError error)
        {
            Debug.LogError($"Banner failed to load for {placementName}: {error.ErrorMessage}");
            BannerLoaded?.Invoke(false);
        }

        private void HandleAdDisplayed(string placementName, LevelPlayAdInfo adInfo)
        {
            Debug.Log($"Banner Displayed for {placementName}");
        }

        private void HandleAdFailedToDisplay(string placementName, LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogError($"Banner {adInfo.AdNetwork} failed to display for {placementName}: {error}");
        }

        private void HandleAdCollapsed(string placementName, LevelPlayAdInfo adInfo)
        {
            Debug.Log($"Banner Collapsed for {placementName}");
        }

        private void HandleAdExpanded(string placementName, LevelPlayAdInfo adInfo)
        {
            Debug.Log($"Banner Expanded for {placementName}");
        }

        public void Dispose()
        {
            foreach (var ad in _bannerAds.Values)
            {
                ad.DestroyAd();
            }
            _bannerAds.Clear();
        }
    }
}
