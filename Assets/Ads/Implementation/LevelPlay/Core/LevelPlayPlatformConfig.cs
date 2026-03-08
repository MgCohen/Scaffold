using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Ads.Configurations
{
    [Serializable]
    public struct LevelPlayPlatformConfig
    {
        public RuntimePlatform platform;
        public string appKey;
        public List<RewardedAdConfig> rewardedPlacements;
        public List<InterstitialAdConfig> interstitialPlacements;
        public List<BannerAdConfig> bannerPlacements;
    }
}