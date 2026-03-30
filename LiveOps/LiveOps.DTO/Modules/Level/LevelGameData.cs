using System;
using System.Collections.Generic;
using System.Linq;
using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Level
{
    /// <summary>
    /// Aggregated level payload returned in <see cref="GameModuleDTO.GameModule.GameData"/>.
    /// </summary>
    public sealed class LevelGameData : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(LevelGameData).Name;

        [JsonProperty]
        private List<LevelStateEntry> _states = new List<LevelStateEntry>();

        [JsonProperty]
        private int _rewardPerLevel;

        /// <summary>One entry per level ID in config order.</summary>
        [JsonIgnore]
        public IReadOnlyList<LevelStateEntry> States => _states;

        /// <summary>Gold granted by server for a successful level completion.</summary>
        [JsonIgnore]
        public int RewardPerLevel => _rewardPerLevel;

        /// <summary>Used by Newtonsoft when deserializing <c>GameData</c>; <see cref="_states"/> is filled from JSON.</summary>
        [JsonConstructor]
        public LevelGameData()
        {
        }

        /// <summary>Build from persistence + config (server).</summary>
        public LevelGameData(LevelPersistence persistence, LevelConfig config)
        {
            if (persistence == null || config == null)
            {
                // Newtonsoft JSON deserializer greedily invokes this constructor with null parameters.
                // We return early and let it populate the fields via reflection parameters.
                return; 
            }

            HashSet<int> completed = new HashSet<int>(persistence.CompletedLevelIds);
            _rewardPerLevel = config.RewardPerLevel;
            _states = config.Levels.Select(id =>
                    new LevelStateEntry(id, InitialState(id, completed)))
                .ToList();

            if (_states.Count > 0 && _states[0].State == LevelAvailabilityState.Blocked)
            {
                _states[0] = new LevelStateEntry(_states[0].LevelId, LevelAvailabilityState.Unlocked);
            }

            for (int i = 0; i < _states.Count - 1; i++)
            {
                if (_states[i].State == LevelAvailabilityState.Complete && _states[i + 1].State == LevelAvailabilityState.Blocked)
                {
                    _states[i + 1] = new LevelStateEntry(_states[i + 1].LevelId, LevelAvailabilityState.Unlocked);
                }
            }
        }

        /// <summary>
        /// Applies a successful completion response locally by marking the completed level as complete
        /// and unlocking the next level when it is currently blocked.
        /// </summary>
        public void ApplyCompletedLevel(int completedLevelId)
        {
            if (_states == null || _states.Count == 0)
            {
                return;
            }

            int index = _states.FindIndex(state => state.LevelId == completedLevelId);
            if (index < 0)
            {
                return;
            }

            LevelStateEntry current = _states[index];
            _states[index] = new LevelStateEntry(current.LevelId, LevelAvailabilityState.Complete);

            int nextIndex = index + 1;
            if (nextIndex < _states.Count && _states[nextIndex].State == LevelAvailabilityState.Blocked)
            {
                LevelStateEntry next = _states[nextIndex];
                _states[nextIndex] = new LevelStateEntry(next.LevelId, LevelAvailabilityState.Unlocked);
            }
        }

        private static LevelAvailabilityState InitialState(int id, HashSet<int> completed)
        {
            return completed.Contains(id) ? LevelAvailabilityState.Complete : LevelAvailabilityState.Blocked;
        }
    }
}
