using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Gold
{
    /// <summary>
    /// Data model for the Gold Reward module.
    /// </summary>
    public class GoldRewardModuleData : IGameModuleData
    {
        /// <summary>Gets the resolved classification name for the component.</summary>
        public string Key { get { return GameDataExtensions.GetKey<GoldRewardModuleData>(); } }

        [JsonProperty]
        private int _reward = 100;

        /// <summary>Gets the gold reward amount.</summary>
        [JsonIgnore]
        public int Reward { get { return _reward; } }

        public void SetReward(int value)
        {
            _reward = value;
        }
    }
}
