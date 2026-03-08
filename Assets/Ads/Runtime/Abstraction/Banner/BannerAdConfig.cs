using System;
using UnityEngine;

namespace Game.Ads.Configurations
{
    [Serializable]
    public class BannerAdConfig : AdPlacementConfig
    {
        [Tooltip("Where the banner should be positioned")]
        public BannerPosition bannerPosition;
    }
}
