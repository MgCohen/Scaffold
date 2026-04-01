using System.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Scaffold.Ads
{
    public class LevelPlayAdProvider : IAdProvider
    {
        private readonly LevelPlayAdConfigurationSO _configuration;
        private readonly LevelPlayPlatformConfig _activeConfig;

        private bool _isInitialized;
        public string UserId
        {
            get;
            set;
        }

        // Internal specialized services
        private LevelPlayRewardedAdService _rewardedService;
        private LevelPlayInterstitialAdService _interstitialService;
        private LevelPlayBannerAdService _bannerService;

        public IRewardedAdService RewardedAdService
        {
            get { return _rewardedService; }
        }

        public IInterstitialAdService InterstitialAdService
        {
            get { return _interstitialService; }
        }

        public IBannerAdService BannerAdService
        {
            get { return _bannerService; }
        }

        public LevelPlayAdProvider(LevelPlayAdConfigurationSO configuration)
        {
            _configuration = configuration;
            _activeConfig = configuration.GetActiveConfiguration();
        }

        public async Awaitable Initialize(string userId)
        {
            UserId = userId;
            string appKey = _activeConfig.appKey;
            
            if (_isInitialized)
            {
                return;
            }
            
            Debug.Log($"Initializing LevelPlay with appKey: '{appKey} ' and userId: '{UserId}'");

            LevelPlay.OnInitSuccess += OnSDKInitialized;
            LevelPlay.OnInitFailed += OnSDKInitializationFailed;


            if (string.IsNullOrEmpty(UserId))
            {
                LevelPlay.Init(appKey);
            }
            else
            {
                LevelPlay.Init(appKey, UserId);
            }

            await Task.Yield();
        }

        public void SetMuted(bool mute)
        {
            // LevelPlay doesn't strictly have a direct mute API globally here
        }

        private void OnSDKInitialized(LevelPlayConfiguration configuration)
        {
            _isInitialized = true;
            Debug.Log("LevelPlay SDK initialized successfully.");

            // Construct and initialize internal services
            _rewardedService = new LevelPlayRewardedAdService(_configuration);
            _rewardedService.Initialize();

            _interstitialService = new LevelPlayInterstitialAdService(_configuration);
            _interstitialService.Initialize();

            _bannerService = new LevelPlayBannerAdService(_configuration);
            _bannerService.Initialize();
        }

        private void OnSDKInitializationFailed(LevelPlayInitError error)
        {
            Debug.LogError($"LevelPlay Initialization Failed: {error.ErrorMessage}");
        }

        public void Dispose()
        {
            LevelPlay.OnInitSuccess -= OnSDKInitialized;
            LevelPlay.OnInitFailed -= OnSDKInitializationFailed;

            _rewardedService?.Dispose();
            _interstitialService?.Dispose();
            _bannerService?.Dispose();
        }
    }
}
