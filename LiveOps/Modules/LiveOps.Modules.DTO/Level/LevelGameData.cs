using System;
using System.Collections.Generic;
using System.Linq;
using LiveOps.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Level
{

    public sealed class LevelGameData : IGameModuleData
    {

        public string Key => typeof(LevelGameData).Name;

        [JsonProperty]
        private List<LevelStateEntry> _states = new List<LevelStateEntry>();

        [JsonProperty]
        private int _rewardPerLevel;

        [JsonIgnore]
        public IReadOnlyList<LevelStateEntry> States => _states;

        [JsonIgnore]
        public int RewardPerLevel => _rewardPerLevel;

        [JsonConstructor]
        public LevelGameData()
        {
        }

        public LevelGameData(LevelPersistence persistence, LevelConfig config)
        {
            if (persistence == null || config == null)
            {

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
