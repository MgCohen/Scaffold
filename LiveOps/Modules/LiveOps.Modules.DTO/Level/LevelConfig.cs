using System;
using System.Collections.Generic;
using LiveOps.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Level
{

    public sealed class LevelConfig : IGameModuleData
    {

        public string Key => typeof(LevelConfig).Name;

        [JsonProperty]
        private List<int> _levels = new List<int>();

        [JsonProperty]
        private int _rewardPerLevel;

        [JsonIgnore]
        public IReadOnlyList<int> Levels => _levels ?? (IReadOnlyList<int>)Array.Empty<int>();

        [JsonIgnore]
        public int RewardPerLevel => _rewardPerLevel;
    }
}
