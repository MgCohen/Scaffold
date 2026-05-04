using System.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Scaffold.Ads.Levelplay
{
    public class LevelPlayAdProvider : IAdProvider
    {
        public LevelPlayAdProvider(LevelPlayAdConfigurationSO configuration)
        {
            this.configuration = configuration;
            activeConfig = configuration.GetActiveConfiguration();
        }

        public string UserId { get; set; }

        public IRewardedAdService RewardedAdService => rewardedService;

        public IInterstitialAdService InterstitialAdService => interstitialService;

        public IBannerAdService BannerAdService => bannerService;

        private readonly LevelPlayAdConfigurationSO configuration;
        private readonly LevelPlayPlatformConfig activeConfig;
        private bool isInitialized;
        private LevelPlayRewardedAdService rewardedService;
        private LevelPlayInterstitialAdService interstitialService;
        private LevelPlayBannerAdService bannerService;

        public async Task Initialize(string userId)
        {
            UserId = userId;
            if (isInitialized)
            {
                return;
            }

            string appKey = activeConfig.AppKey;
            Debug.Log($"Initializing LevelPlay with appKey: '{appKey} ' and userId: '{UserId}'");

            LevelPlay.OnInitSuccess += OnSDKInitialized;
            LevelPlay.OnInitFailed += OnSDKInitializationFailed;

            StartLevelPlaySdk(appKey);
            await Task.Yield();
        }

        public void SetMuted(bool mute)
        {
            // LevelPlay doesn't strictly have a direct mute API globally here
        }

        public void Dispose()
        {
            LevelPlay.OnInitSuccess -= OnSDKInitialized;
            LevelPlay.OnInitFailed -= OnSDKInitializationFailed;

            rewardedService?.Dispose();
            interstitialService?.Dispose();
            bannerService?.Dispose();
        }

        private void StartLevelPlaySdk(string appKey)
        {
            if (string.IsNullOrEmpty(UserId))
            {
                LevelPlay.Init(appKey);
            }
            else
            {
                LevelPlay.Init(appKey, UserId);
            }
        }

        private void OnSDKInitialized(LevelPlayConfiguration sdkInitConfiguration)
        {
            isInitialized = true;
            Debug.Log($"LevelPlay SDK initialized successfully. (config present: {sdkInitConfiguration != null})");

            rewardedService = new LevelPlayRewardedAdService(this.configuration);
            rewardedService.Initialize();

            interstitialService = new LevelPlayInterstitialAdService(this.configuration);
            interstitialService.Initialize();

            bannerService = new LevelPlayBannerAdService(this.configuration);
            bannerService.Initialize();
        }

        private void OnSDKInitializationFailed(LevelPlayInitError error)
        {
            Debug.LogError($"LevelPlay Initialization Failed: {error.ErrorMessage}");
        }
    }
}
