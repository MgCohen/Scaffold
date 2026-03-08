using System;

namespace Game.Ads.Endpoint
{
    [Serializable]
    public class RewardRequestPayload
    {
        public string unityUserId;
        public string rewardAdId;
    }
}