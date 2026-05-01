using System;
using UnityEngine;

namespace Scaffold.Ads
{
    public class AdManager : IDisposable
    {
        public AdManager(AdConfigurationSO adConfiguration, RewardedAdManager rewardedAdManager, InterstitialAdManager interstitialAdManager, BannerAdManager bannerAdManager)
        {
            this.adConfiguration = adConfiguration;
            this.rewardedAdManager = rewardedAdManager;
            this.interstitialAdManager = interstitialAdManager;
            this.bannerAdManager = bannerAdManager;

            if (this.adConfiguration == null)
            {
                Debug.LogError("AdConfigurationSO is not set on AdManager.");
                return;
            }

            adProvider = this.adConfiguration.CreateProvider();
        }

        public AdConfigurationSO Configuration => adConfiguration;
        public RewardedAdManager RewardedAds => rewardedAdManager;
        public InterstitialAdManager InterstitialAds => interstitialAdManager;
        public BannerAdManager BannerAds => bannerAdManager;

        private readonly AdConfigurationSO adConfiguration;

        private readonly RewardedAdManager rewardedAdManager;
        private readonly InterstitialAdManager interstitialAdManager;
        private readonly BannerAdManager bannerAdManager;

        private IAdProvider adProvider;
        private bool isInitialized;

        public async void InitializeAds(string userId, IRewardEndpointClient rewardClient)
        {
            if (isInitialized)
            {
                return;
            }
            isInitialized = true;

            SetRewardEndpointClient(rewardClient);

            await adProvider.Initialize(userId);

            // Give the active ad services to their respective managers
            InitializeRewardedAdManager();
            InitializeInterstitialAdManager();
            InitializeBannerAdManager();
        }

        public void SetRewardEndpointClient(IRewardEndpointClient rewardClient)
        {
            if (rewardedAdManager != null)
            {
                if (rewardClient != null)
                {
                    rewardedAdManager.SetRewardEndpointClient(rewardClient);
                }
            }
        }

        private void InitializeRewardedAdManager()
        {
            if (adProvider.RewardedAdService != null)
            {
                rewardedAdManager.Initialize(adProvider.RewardedAdService, adConfiguration, adProvider.UserId);
            }
        }

        private void InitializeInterstitialAdManager()
        {
            if (adProvider.InterstitialAdService != null)
            {
                interstitialAdManager.Initialize(adProvider.InterstitialAdService, adConfiguration);
            }
        }

        private void InitializeBannerAdManager()
        {
            if (adProvider.BannerAdService != null)
            {
                bannerAdManager.Initialize(adProvider.BannerAdService, adConfiguration);
            }
        }

        public void Dispose()
        {
            adProvider?.Dispose();
        }
    }
}
