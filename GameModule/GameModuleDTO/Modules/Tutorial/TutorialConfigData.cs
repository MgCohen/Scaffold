using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Common;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Tutorial
{
    /// <summary>
    /// Configuration data for the Tutorial module.
    /// </summary>
    public class TutorialConfigData : IGameModuleData, IIsActive
    {
        public string Key { get { return GameDataExtensions.GetKey<TutorialConfigData>(); } }

        [JsonProperty]
        private bool _isActive = true;

        [JsonIgnore]
        public bool IsActive => _isActive;

        public void SetActive(bool value)
        {
            _isActive = value;
        }

        [JsonProperty]
        private long _reward = 300;

        /// <summary>Gets the gold reward amount for completing a tutorial.</summary>
        [JsonIgnore]
        public long Reward => _reward;

        public void SetReward(long value)
        {
            _reward = value;
        }

        [JsonProperty]
        private List<int> _tutorials = new List<int>();

        /// <summary>Gets the list of valid tutorial step IDs.</summary>
        [JsonIgnore]
        public List<int> Tutorials => _tutorials;

        public void SetTutorials(List<int> value)
        {
            _tutorials = value;
        }
    }
}
