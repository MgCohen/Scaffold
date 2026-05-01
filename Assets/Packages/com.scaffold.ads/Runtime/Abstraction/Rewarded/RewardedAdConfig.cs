using System;
using UnityEngine;

namespace Scaffold.Ads
{
    [Serializable]
    public class RewardedAdConfig : AdPlacementConfig
    {
        [Tooltip("The secure endpoint URL to hit upon reward completion")]
        public string RewardEndpointUrl;
    }
}
