using System.Collections.Generic;
using System.Linq;
using LiveOps.Core.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Level
{
    /// <summary>
    /// Player-persisted level progress (minimal: completed IDs and optional last selection).
    /// </summary>
    public sealed class LevelPersistence : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(LevelPersistence).Name;

        [JsonProperty]
        private List<int> _completedLevelIds = new List<int>();

        [JsonProperty]
        private int? _lastSelectedLevelId;

        /// <summary>Gets distinct completed level IDs (order not guaranteed).</summary>
        [JsonIgnore]
        public IReadOnlyList<int> CompletedLevelIds => _completedLevelIds;

        /// <summary>Gets optional last selected level for client UX.</summary>
        [JsonIgnore]
        public int? LastSelectedLevelId => _lastSelectedLevelId;

        /// <summary>Records a completed level if not already present.</summary>
        public void AddCompletedLevel(int levelId)
        {
            if (!_completedLevelIds.Contains(levelId))
            {
                _completedLevelIds.Add(levelId);
            }

            NormalizeCompletedDistinct();
        }

        /// <summary>Sets last selected level ID for UX.</summary>
        public void SetLastSelectedLevelId(int? levelId)
        {
            _lastSelectedLevelId = levelId;
        }

        private void NormalizeCompletedDistinct()
        {
            _completedLevelIds = _completedLevelIds.Distinct().ToList();
        }
    }
}
