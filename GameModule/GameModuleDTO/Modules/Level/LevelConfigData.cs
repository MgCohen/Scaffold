using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Common;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Level
{
    /// <summary>
    /// Configuration data for the Level module.
    /// </summary>
    public class LevelConfigData : IGameModuleData, IIsActive
    {
        public string Key { get { return GameDataExtensions.GetKey<LevelConfigData>(); } }

        [JsonProperty]
        private bool _isActive = true;

        [JsonIgnore]
        public bool IsActive => _isActive;

        public void SetActive(bool value)
        {
            _isActive = value;
        }

        [JsonProperty]
        private long _reward = 200;

        /// <summary>Gets the gold reward amount for completing a level.</summary>
        [JsonIgnore]
        public long Reward => _reward;

        public void SetReward(long value)
        {
            _reward = value;
        }

        [JsonProperty]
        private List<int> _levels = new List<int>();

        /// <summary>Gets the list of valid level IDs.</summary>
        [JsonIgnore]
        public List<int> Levels => _levels;

        public void SetLevels(List<int> value)
        {
            _levels = value;
        }
    }
}
