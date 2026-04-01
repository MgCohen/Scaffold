using System;
using UnityEngine;

namespace Scaffold.Ads
{
    public class BannerAdManager : MonoBehaviour
    {
        private AdConfigurationSO _adConfiguration;
        private IBannerAdService _adService;

        public event Action<bool> BannerLoaded;

        private bool _isInitialized;

        public void Initialize(IBannerAdService bannerAdService, AdConfigurationSO config)
        {
            if (_isInitialized)
            {
                return;
            }

            _adService = bannerAdService;
            _adConfiguration = config;

            _adService.BannerLoaded += HandleBannerLoaded;

            _isInitialized = true;
        }

        public void LoadBanner(string placementName = null)
        {
            if (!_isInitialized || _adService == null)
            {
                Debug.LogWarning("BannerAdManager not initialized");
                return;
            }

            _adService.LoadBanner(placementName);
        }

        public void ShowBanner(string placementName = null, BannerPosition? position = null)
        {
            if (!_isInitialized || _adService == null)
            {
                return;
            }

            _adService.ShowBanner(placementName, position);
        }

        public void HideBanner(string placementName = null)
        {
            if (!_isInitialized || _adService == null) return;

            _adService.HideBanner(placementName);
        }

        public void DestroyBanner(string placementName = null)
        {
            if (!_isInitialized || _adService == null) return;

            _adService.DestroyBanner(placementName);
        }

        private void HandleBannerLoaded(bool loaded)
        {
            BannerLoaded?.Invoke(loaded);
        }

        private void OnDestroy()
        {
            if (_adService != null)
            {
                _adService.BannerLoaded -= HandleBannerLoaded;
            }
        }
    }
}
