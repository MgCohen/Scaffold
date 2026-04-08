using UnityEngine;
using VContainer;
using Scaffold.Ads;
using Scaffold.LiveOps;

namespace Scaffold.Ads.Levelplay.Test
{
    public class UnityAdsTester : MonoBehaviour
    {
        [Header("Ad System Components")]
        public AdManager AdManager;

        [Header("Testers (Optional Auto-Inject)")]
        public RewardedAdTester RewardedAdTester;
        public InterstitialAdTester InterstitialAdTester;
        public BannerAdTester BannerAdTester;

        [Header("System Dependencies")]
        [Inject]
        private ILiveOpsService liveOpsService;

        private void Start()
        {
            Initialize();
        }

        private void Initialize(string userId = "")
        {
            if (AdManager == null)
            {
                return;
            }

            AdManager.InitializeAds(userId, CreateRewardEndpointClient());
            AssignAdManagerToTesters();
        }

        private void AssignAdManagerToTesters()
        {
            if (RewardedAdTester != null && RewardedAdTester.AdManager == null)
            {
                RewardedAdTester.AdManager = AdManager;
            }

            if (InterstitialAdTester != null && InterstitialAdTester.AdManager == null)
            {
                InterstitialAdTester.AdManager = AdManager;
            }

            if (BannerAdTester != null && BannerAdTester.AdManager == null)
            {
                BannerAdTester.AdManager = AdManager;
            }
        }

        private IRewardEndpointClient CreateRewardEndpointClient()
        {
            if (liveOpsService != null)
            {
                return new LiveOpsRewardEndpointClient(liveOpsService);
            }

            return new HttpRewardEndpointClient(AdManager.Configuration.FallbackRewardEndpointUrl);
        }
    }
}
