using System.Collections.Generic;
using LiveOps.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Ads
{

    public sealed class AdsConfig : IGameModuleData
    {

        public string Key => typeof(AdsConfig).Name;

        [JsonProperty]
        private Dictionary<string, AdPlacementConfig> _placements = new Dictionary<string, AdPlacementConfig>();

        [JsonIgnore]
        public IReadOnlyDictionary<string, AdPlacementConfig> Placements => _placements;

        public AdPlacementConfig GetPlacement(string placementId)
        {
            if (string.IsNullOrEmpty(placementId)) return new AdPlacementConfig();
            return _placements.TryGetValue(placementId, out var config) ? config : new AdPlacementConfig();
        }
    }
}
