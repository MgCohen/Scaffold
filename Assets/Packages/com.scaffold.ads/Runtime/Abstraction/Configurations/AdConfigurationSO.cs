using UnityEngine;
using System.Collections.Generic;

namespace Scaffold.Ads
{
    /// <summary>
    /// Base configuration for ad services. Implementations will provide
    /// specific IAdProvider instances configured with their required keys.
    /// </summary>
    public abstract class AdConfigurationSO : ScriptableObject
    {
        [Header("Fallback Reward Endpoint")]
        [Tooltip("HTTP endpoint URL used when LiveOps is not available")]
        [SerializeField] private string fallbackRewardEndpointUrl;

        public string FallbackRewardEndpointUrl => fallbackRewardEndpointUrl;

        public abstract List<RewardedAdConfig> GetRewardedPlacements();
        public abstract List<InterstitialAdConfig> GetInterstitialPlacements();
        public abstract List<BannerAdConfig> GetBannerPlacements();

        public virtual bool GetRewardedPlacement(string placementName, out RewardedAdConfig placement)
        {
            List<RewardedAdConfig> placements = GetRewardedPlacements();
            if (placements != null)
            {
                foreach (RewardedAdConfig config in placements)
                {
                    if (config.placementKey == placementName)
                    {
                        placement = config;
                        return true;
                    }
                }
            }

            placement = null;
            return false;
        }

        public abstract IAdProvider CreateProvider();
    }
}
