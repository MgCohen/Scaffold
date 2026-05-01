using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Ads.Levelplay
{
    [CreateAssetMenu(fileName = "LevelPlayAdConfiguration", menuName = "Ads/LevelPlay Configuration")]
    public class LevelPlayAdConfigurationSO : AdConfigurationSO
    {
        [Header("Editor Configuration")]
        [Tooltip("The configuration used when running in the Unity Editor.")]
        public LevelPlayPlatformConfig EditorConfig;

        [Header("Platform Configurations")]
        public List<LevelPlayPlatformConfig> PlatformConfigs = new List<LevelPlayPlatformConfig>();

        public override List<RewardedAdConfig> GetRewardedPlacements()
        {
            LevelPlayPlatformConfig activeConfig = GetActiveConfiguration();
            return activeConfig.RewardedPlacements ?? new List<RewardedAdConfig>();
        }

        public override List<InterstitialAdConfig> GetInterstitialPlacements()
        {
            LevelPlayPlatformConfig activeConfig = GetActiveConfiguration();
            return activeConfig.InterstitialPlacements ?? new List<InterstitialAdConfig>();
        }

        public override List<BannerAdConfig> GetBannerPlacements()
        {
            LevelPlayPlatformConfig activeConfig = GetActiveConfiguration();
            return activeConfig.BannerPlacements ?? new List<BannerAdConfig>();
        }

        public override IAdProvider CreateProvider()
        {
            return new LevelPlayAdProvider(this);
        }

        public LevelPlayPlatformConfig GetActiveConfiguration()
        {
            if (Application.isEditor)
            {
                return EditorConfig;
            }

            foreach (LevelPlayPlatformConfig config in PlatformConfigs)
            {
                if (config.Platform == Application.platform)
                {
                    return config;
                }
            }

            Debug.LogWarning($"[LevelPlay] No config found for platform: {Application.platform}. Falling back to Editor config.");
            return EditorConfig;
        }
    }
}
