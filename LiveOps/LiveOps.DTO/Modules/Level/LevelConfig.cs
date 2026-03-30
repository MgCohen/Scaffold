using System;
using System.Collections.Generic;
using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Level
{
    /// <summary>
    /// Remote configuration: ordered level IDs (order matches config array order).
    /// </summary>
    public sealed class LevelConfig : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(LevelConfig).Name;

        [JsonProperty]
        private List<int> _levels = new List<int>();

        /// <summary>Gold granted on every successful level completion (same amount for all levels).</summary>
        [JsonProperty]
        private int _rewardPerLevel;

        /// <summary>Level IDs in remote-config order (index defines progression, not numeric id).</summary>
        [JsonIgnore]
        public IReadOnlyList<int> Levels => _levels ?? (IReadOnlyList<int>)Array.Empty<int>();

        /// <summary>Gold granted on every successful level completion (same for all levels in <see cref="Levels"/>).</summary>
        [JsonIgnore]
        public int RewardPerLevel => _rewardPerLevel;
    }
}
