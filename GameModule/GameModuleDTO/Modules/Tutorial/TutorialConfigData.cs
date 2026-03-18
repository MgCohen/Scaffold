using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Common;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Tutorial
{
    /// <summary>
    /// Configuration data for the Tutorial module.
    /// </summary>
    public class TutorialConfigData : BaseModuleConfigData
    {
        public override string Key { get { return GameDataExtensions.GetKey<TutorialConfigData>(); } }

        [JsonProperty]
        private long _reward = 300;

        /// <summary>Gets the gold reward amount for completing a tutorial.</summary>
        [JsonIgnore]
        public long Reward => _reward;

        public void SetReward(long value)
        {
            _reward = value;
        }
    }
}
