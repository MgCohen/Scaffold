using System;
using UnityEngine;

namespace Scaffold.Ads
{
    [Serializable]
    public class BannerAdConfig : AdPlacementConfig
    {
        [Tooltip("Where the banner should be positioned")]
        public BannerPosition bannerPosition;
    }
}
