using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Ads.Levelplay
{
    [Serializable]
    public struct LevelPlayPlatformConfig
    {
        public RuntimePlatform Platform;
        public string AppKey;
        public List<RewardedAdConfig> RewardedPlacements;
        public List<InterstitialAdConfig> InterstitialPlacements;
        public List<BannerAdConfig> BannerPlacements;
    }
}
