using System;
using System.Collections.Generic;
using LiveOps.DTO.Keys;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Level
{
    [LiveOpsKey("LevelConfig")]
    public sealed class LevelConfig
    {
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
