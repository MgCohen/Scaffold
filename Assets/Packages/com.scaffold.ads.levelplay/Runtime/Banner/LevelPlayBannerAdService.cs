using System;
using System.Collections.Generic;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Scaffold.Ads.Levelplay
{
    public class LevelPlayBannerAdService : IBannerAdService, IDisposable
    {
        public LevelPlayBannerAdService(LevelPlayAdConfigurationSO configuration)
        {
            this.configuration = configuration;
        }

        private readonly LevelPlayAdConfigurationSO configuration;
        private readonly Dictionary<string, LevelPlayBannerAd> bannerAds = new Dictionary<string, LevelPlayBannerAd>();

        public event Action<bool> BannerLoaded;

        public void Initialize()
        {
            List<BannerAdConfig> activePlacements = configuration.GetBannerPlacements();
            if (activePlacements == null)
            {
                return;
            }

            foreach (BannerAdConfig placement in activePlacements)
            {
                RegisterBannerPlacement(placement);
            }
        }

        private void RegisterBannerPlacement(BannerAdConfig placement)
        {
            string unitId = placement.AdUnitId;
            if (string.IsNullOrEmpty(unitId))
            {
                return;
            }

            string placementKey = placement.PlacementKey;
            if (bannerAds.ContainsKey(placementKey))
            {
                return;
            }

            LevelPlayBannerAd ad = CreateBannerAd(placement, unitId, placementKey);
            bannerAds[placementKey] = ad;
        }

        private LevelPlayBannerAd CreateBannerAd(BannerAdConfig placement, string unitId, string placementKey)
        {
            LevelPlayBannerPosition position = GetLevelPlayPosition(placement.BannerPosition);
            LevelPlayBannerAd.Config config = new LevelPlayBannerAd.Config.Builder().SetPosition(position).Build();
            LevelPlayBannerAd ad = new LevelPlayBannerAd(unitId, config);
            WireBannerAdEvents(ad, placementKey);
            return ad;
        }

        private void WireBannerAdEvents(LevelPlayBannerAd ad, string placementKey)
        {
            ad.OnAdLoaded += (adInfo) => HandleAdLoadedSuccessfully(placementKey, adInfo);
            ad.OnAdLoadFailed += (error) => HandleAdLoadFailed(placementKey, error);
            ad.OnAdDisplayed += (adInfo) => HandleAdDisplayed(placementKey, adInfo);
            ad.OnAdDisplayFailed += (adInfo, error) => HandleAdFailedToDisplay(placementKey, adInfo, error);
            ad.OnAdCollapsed += (adInfo) => HandleAdCollapsed(placementKey, adInfo);
            ad.OnAdExpanded += (adInfo) => HandleAdExpanded(placementKey, adInfo);
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
            LevelPlayBannerAd targetAd = GetTargetAd(placementName);
            if (targetAd != null)
            {
                targetAd.LoadAd();
            }
        }

        public void ShowBanner(string placementName = null, BannerPosition? position = null)
        {
            LevelPlayBannerAd targetAd = GetTargetAd(placementName);
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
            LevelPlayBannerAd targetAd = GetTargetAd(placementName);
            targetAd?.HideAd();
        }

        public void DestroyBanner(string placementName = null)
        {
            LevelPlayBannerAd targetAd = GetTargetAd(placementName);
            if (targetAd != null)
            {
                targetAd.DestroyAd();
            }
        }

        private LevelPlayBannerAd GetTargetAd(string placementName)
        {
            if (string.IsNullOrEmpty(placementName))
            {
                foreach (LevelPlayBannerAd ad in bannerAds.Values)
                {
                    return ad;
                }
                return null;
            }

            bannerAds.TryGetValue(placementName, out LevelPlayBannerAd targetAd);
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
            foreach (LevelPlayBannerAd ad in bannerAds.Values)
            {
                ad.DestroyAd();
            }
            bannerAds.Clear();
        }
    }
}
