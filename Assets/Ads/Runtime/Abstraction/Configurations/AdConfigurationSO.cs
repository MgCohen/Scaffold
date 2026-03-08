using Game.Ads.Interfaces;
using UnityEngine;
using System.Collections.Generic;

namespace Game.Ads.Configurations
{
    /// <summary>
    /// Base configuration for ad services. Implementations will provide
    /// specific IAdProvider instances configured with their required keys.
    /// </summary>
    public abstract class AdConfigurationSO : ScriptableObject
    {
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
