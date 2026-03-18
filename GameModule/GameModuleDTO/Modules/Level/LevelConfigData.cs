using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Common;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Level
{
    /// <summary>
    /// Configuration data for the Level module.
    /// </summary>
    public class LevelConfigData : BaseModuleConfigData
    {
        public override string Key { get { return GameDataExtensions.GetKey<LevelConfigData>(); } }

        [JsonProperty]
        private long _reward = 200;

        /// <summary>Gets the gold reward amount for completing a level.</summary>
        [JsonIgnore]
        public long Reward => _reward;

        public void SetReward(long value)
        {
            _reward = value;
        }
    }
}
