using System.Collections.Generic;
using System.Linq;
using LiveOps.DTO.Keys;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Level
{
    [LiveOpsKey("LevelPersistence")]
    public sealed class LevelPersistence
    {
        [JsonProperty]
        private List<int> _completedLevelIds = new List<int>();

        [JsonProperty]
        private int? _lastSelectedLevelId;

        [JsonIgnore]
        public IReadOnlyList<int> CompletedLevelIds => _completedLevelIds;

        [JsonIgnore]
        public int? LastSelectedLevelId => _lastSelectedLevelId;

        public void AddCompletedLevel(int levelId)
        {
            if (!_completedLevelIds.Contains(levelId))
            {
                _completedLevelIds.Add(levelId);
            }

            NormalizeCompletedDistinct();
        }

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
