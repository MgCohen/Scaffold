using System;
using UnityEngine;

namespace Game.Ads.Configurations
{
    [Serializable]
    public class RewardedAdConfig : AdPlacementConfig
    {
        [Tooltip("The secure endpoint URL to hit upon reward completion")]
        public string rewardEndpointUrl;
    }
}
