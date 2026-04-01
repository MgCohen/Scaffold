using System;
using UnityEngine;

namespace Scaffold.Ads
{
    /// <summary>
    /// Serves as the global entry point for all advertising logic.
    /// Manages initialization via IAdProvider and delegates types to specific managers.
    /// </summary>
    public class AdManager : IDisposable
    {
        private readonly AdConfigurationSO _adConfiguration;

        // Specialized Managers
        private readonly RewardedAdManager _rewardedAdManager;
        private readonly InterstitialAdManager _interstitialAdManager;
        private readonly BannerAdManager _bannerAdManager;

        private IAdProvider _adProvider;
        private bool _isInitialized;

        public RewardedAdManager RewardedAds => _rewardedAdManager;
        public InterstitialAdManager InterstitialAds => _interstitialAdManager;
        public BannerAdManager BannerAds => _bannerAdManager;

        public AdManager(AdConfigurationSO adConfiguration, RewardedAdManager rewardedAdManager, InterstitialAdManager interstitialAdManager, BannerAdManager bannerAdManager)
        {
            _adConfiguration = adConfiguration;
            _rewardedAdManager = rewardedAdManager;
            _interstitialAdManager = interstitialAdManager;
            _bannerAdManager = bannerAdManager;

            if (_adConfiguration == null)
            {
                Debug.LogError("AdConfigurationSO is not set on AdManager.");
                return;
            }

            _adProvider = _adConfiguration.CreateProvider();
        }

        public void SetRewardEndpointClient(IRewardEndpointClient rewardClient)
        {
            if (_rewardedAdManager != null)
            {
                if (rewardClient != null)
                {
                    _rewardedAdManager.SetRewardEndpointClient(rewardClient);
                }
            }
        }

        public async void InitializeAds(string userId, IRewardEndpointClient rewardClient)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;

            SetRewardEndpointClient(rewardClient);

            await _adProvider.Initialize(userId);

            // Give the active ad services to their respective managers
            InitializeRewardedAdManager();
            InitializeInterstitialAdManager();
            InitializeBannerAdManager();
        }

        private void InitializeRewardedAdManager()
        {
            if (_adProvider.RewardedAdService != null)
            {
                _rewardedAdManager.Initialize(_adProvider.RewardedAdService, _adConfiguration, _adProvider.UserId);
            }
        }

        private void InitializeInterstitialAdManager()
        {
            if (_adProvider.InterstitialAdService != null)
            {
                _interstitialAdManager.Initialize(_adProvider.InterstitialAdService, _adConfiguration);
            }
        }

        private void InitializeBannerAdManager()
        {
            if (_adProvider.BannerAdService != null)
            {
                _bannerAdManager.Initialize(_adProvider.BannerAdService, _adConfiguration);
            }
        }

        public void Dispose()
        {
            _adProvider?.Dispose();
        }
    }
}
