using System;
using UnityEngine;

namespace Scaffold.Ads
{
    public class BannerAdManager : IDisposable
    {
        private IBannerAdService adService;
        private bool isInitialized;

        public event Action<bool> BannerLoaded;

        public void Initialize(IBannerAdService bannerAdService, AdConfigurationSO _)
        {
            if (isInitialized)
            {
                return;
            }

            adService = bannerAdService;

            adService.BannerLoaded += HandleBannerLoaded;

            isInitialized = true;
        }

        public void LoadBanner(string placementName = null)
        {
            if (!isInitialized || adService == null)
            {
                Debug.LogWarning("BannerAdManager not initialized");
                return;
            }

            adService.LoadBanner(placementName);
        }

        public void ShowBanner(string placementName = null, BannerPosition? position = null)
        {
            if (!isInitialized || adService == null)
            {
                return;
            }

            adService.ShowBanner(placementName, position);
        }

        public void HideBanner(string placementName = null)
        {
            if (!isInitialized || adService == null)
            {
                return;
            }

            adService.HideBanner(placementName);
        }

        public void DestroyBanner(string placementName = null)
        {
            if (!isInitialized || adService == null)
            {
                return;
            }

            adService.DestroyBanner(placementName);
        }

        private void HandleBannerLoaded(bool loaded)
        {
            BannerLoaded?.Invoke(loaded);
        }

        public void Dispose()
        {
            if (adService != null)
            {
                adService.BannerLoaded -= HandleBannerLoaded;
            }
        }
    }
}
