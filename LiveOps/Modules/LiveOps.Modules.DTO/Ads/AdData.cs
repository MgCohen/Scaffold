using System;
using System.Collections.Generic;
using LiveOps.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Ads
{
    /// <summary>
    /// Client-facing ads payload in <see cref="LiveOps.DTO.GameModule.GameData"/> (built from persistence + config on the server).
    /// </summary>
    public sealed class AdData : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(AdData).Name;

        [JsonProperty]
        private Dictionary<string, AdPlacementClientData> _placements = new Dictionary<string, AdPlacementClientData>();

        [JsonIgnore]
        public IReadOnlyDictionary<string, AdPlacementClientData> Placements => _placements;

        /// <summary>Used by Newtonsoft when deserializing <c>GameData</c>.</summary>
        [JsonConstructor]
        private AdData()
        {
        }

        /// <summary>Build from persistence + config (server).</summary>
        public AdData(AdsPersistence persistence, AdsConfig config)
        {
            if (persistence == null) throw new ArgumentNullException(nameof(persistence));
            if (config == null) throw new ArgumentNullException(nameof(config));

            foreach (var kvp in config.Placements)
            {
                string placementId = kvp.Key;
                AdPlacementConfig placementConfig = kvp.Value;
                AdPlacementState placementState = persistence.GetOrCreateState(placementId);

                _placements[placementId] = new AdPlacementClientData
                {
                    CooldownSeconds = placementConfig.CooldownSeconds,
                    MaxViews = placementConfig.MaxViews,
                    WatchCount = placementState.WatchCount,
                    HasReachedMaxViews = persistence.HasReachedMaxViews(placementId, placementConfig.MaxViews),
                    NextAdAvailableUtc = persistence.ComputeNextAdAvailableUtcIso(placementId, placementConfig.CooldownSeconds),
                    RewardType = placementConfig.RewardType,
                    RewardAmount = placementConfig.RewardAmount
                };
            }
        }

        public AdPlacementClientData GetPlacementData(string placementId)
        {
            if (string.IsNullOrEmpty(placementId)) placementId = "default";
            if (_placements.TryGetValue(placementId, out var data)) return data;

            return new AdPlacementClientData { CooldownSeconds = 0, NextAdAvailableUtc = string.Empty };
        }
    }
}
