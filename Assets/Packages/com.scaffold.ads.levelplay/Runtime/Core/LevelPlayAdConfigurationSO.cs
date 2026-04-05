using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Ads.Levelplay
{
    [CreateAssetMenu(fileName = "LevelPlayAdConfiguration", menuName = "Ads/LevelPlay Configuration")]
    public class LevelPlayAdConfigurationSO : AdConfigurationSO
    {
        [Header("Editor Configuration")]
        [Tooltip("The configuration used when running in the Unity Editor.")]
        public LevelPlayPlatformConfig editorConfig;

        [Header("Platform Configurations")]
        public List<LevelPlayPlatformConfig> platformConfigs = new List<LevelPlayPlatformConfig>();

        public LevelPlayPlatformConfig GetActiveConfiguration()
        {
            if (Application.isEditor)
            {
                return editorConfig;
            }

            foreach (LevelPlayPlatformConfig config in platformConfigs)
            {
                if (config.platform == Application.platform)
                {
                    return config;
                }
            }

            Debug.LogWarning($"[LevelPlay] No config found for platform: {Application.platform}. Falling back to Editor config.");
            return editorConfig;
        }

        public override IAdProvider CreateProvider()
        {
            return new LevelPlayAdProvider(this);
        }

        public override List<RewardedAdConfig> GetRewardedPlacements()
        {
            LevelPlayPlatformConfig activeConfig = GetActiveConfiguration();
            return activeConfig.rewardedPlacements ?? new List<RewardedAdConfig>();
        }

        public override List<InterstitialAdConfig> GetInterstitialPlacements()
        {
            LevelPlayPlatformConfig activeConfig = GetActiveConfiguration();
            return activeConfig.interstitialPlacements ?? new List<InterstitialAdConfig>();
        }

        public override List<BannerAdConfig> GetBannerPlacements()
        {
            LevelPlayPlatformConfig activeConfig = GetActiveConfiguration();
            return activeConfig.bannerPlacements ?? new List<BannerAdConfig>();
        }
    }
}
