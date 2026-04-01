using UnityEngine;

namespace Scaffold.Ads.Test
{
    /// <summary>
    /// Handles the Initialization of the GlobalAdManager and injects it into specific testers.
    /// </summary>
    public class UnityAdsTester : MonoBehaviour
    {
        [Header("Ad System Components")]
        public AdManager adManager;

        [Header("Testers (Optional Auto-Inject)")]
        public RewardedAdTester rewardedAdTester;
        public InterstitialAdTester interstitialAdTester;
        public BannerAdTester bannerAdTester;

        private void Start()
        {
            Initialize();
        }

        private void Initialize(string userId = "")
        {
            if (adManager != null)
            {
                adManager.InitializeAds(userId, new RewardEndpointClient());

                // Auto-inject AdManager into testers if they are assigned but missing the reference
                if (rewardedAdTester != null && rewardedAdTester.adManager == null)
                {
                    rewardedAdTester.adManager = adManager;
                }

                if (interstitialAdTester != null && interstitialAdTester.adManager == null)
                {
                    interstitialAdTester.adManager = adManager;
                }

                if (bannerAdTester != null && bannerAdTester.adManager == null)
                {
                    bannerAdTester.adManager = adManager;
                }
            }
        }
    }
}
