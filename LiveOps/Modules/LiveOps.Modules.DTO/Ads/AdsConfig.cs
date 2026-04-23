using System.Collections.Generic;
using LiveOps.Core.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Ads
{
    /// <summary>
    /// Remote configuration for the Ads module (merged from remote config service).
    /// </summary>
    public sealed class AdsConfig : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(AdsConfig).Name;

        [JsonProperty]
        private Dictionary<string, AdPlacementConfig> _placements = new Dictionary<string, AdPlacementConfig>();

        /// <summary>Gets the configuration per placement.</summary>
        [JsonIgnore]
        public IReadOnlyDictionary<string, AdPlacementConfig> Placements => _placements;

        /// <summary>Gets a specific placement configuration or default if not found.</summary>
        public AdPlacementConfig GetPlacement(string placementId)
        {
            if (string.IsNullOrEmpty(placementId)) return new AdPlacementConfig();
            return _placements.TryGetValue(placementId, out var config) ? config : new AdPlacementConfig();
        }
    }
}
