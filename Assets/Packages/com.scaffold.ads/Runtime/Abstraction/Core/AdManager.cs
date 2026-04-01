using UnityEngine;

namespace Scaffold.Ads
{
    /// <summary>
    /// Serves as the global entry point for all advertising logic.
    /// Manages initialization via IAdProvider and delegates types to specific managers.
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private AdConfigurationSO _adConfiguration;

        // Specialized Managers
        private RewardedAdManager _rewardedAdManager;
        private InterstitialAdManager _interstitialAdManager;
        private BannerAdManager _bannerAdManager;

        private IAdProvider _adProvider;
        private bool _isInitialized;

        public RewardedAdManager RewardedAds
        {
            get { return _rewardedAdManager; }
        }

        public InterstitialAdManager InterstitialAds
        {
            get { return _interstitialAdManager; }
        }

        public BannerAdManager BannerAds
        {
            get { return _bannerAdManager; }
        }

        private void Awake()
        {
            if (_adConfiguration == null)
            {
                Debug.LogError("AdConfigurationSO is not set on GlobalAdManager.");
                return;
            }

            _adProvider = _adConfiguration.CreateProvider();

            // Construct child managers
            _rewardedAdManager = gameObject.AddComponent<RewardedAdManager>();
            _interstitialAdManager = gameObject.AddComponent<InterstitialAdManager>();
            _bannerAdManager = gameObject.AddComponent<BannerAdManager>();
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

        private void OnDestroy()
        {
            _adProvider?.Dispose();
        }
    }
}
