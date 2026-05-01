using UnityEngine;
using System.Collections.Generic;

namespace Scaffold.Ads
{
    public abstract class AdConfigurationSO : ScriptableObject
    {
        public string FallbackRewardEndpointUrl => fallbackRewardEndpointUrl;

        [Header("Fallback Reward Endpoint")]
        [Tooltip("HTTP endpoint URL used when LiveOps is not available")]
        [SerializeField]
        private string fallbackRewardEndpointUrl;

        public virtual bool GetRewardedPlacement(string placementName, out RewardedAdConfig placement)
        {
            List<RewardedAdConfig> placements = GetRewardedPlacements();
            if (placements != null)
            {
                foreach (RewardedAdConfig config in placements)
                {
                    if (config.PlacementKey == placementName)
                    {
                        placement = config;
                        return true;
                    }
                }
            }

            placement = null;
            return false;
        }

        public abstract List<RewardedAdConfig> GetRewardedPlacements();
        public abstract List<InterstitialAdConfig> GetInterstitialPlacements();
        public abstract List<BannerAdConfig> GetBannerPlacements();

        public abstract IAdProvider CreateProvider();
    }
}
