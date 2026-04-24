using System;
using System.Collections.Generic;
using LiveOps.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Ads
{

    public sealed class AdsPersistence : IGameModuleData
    {

        public string Key => typeof(AdsPersistence).Name;

        [JsonProperty]
        private Dictionary<string, AdPlacementState> _settings = new Dictionary<string, AdPlacementState>();

        [JsonIgnore]
        public IReadOnlyDictionary<string, AdPlacementState> Settings => _settings;

        public AdPlacementState GetOrCreateState(string placementId)
        {
            if (string.IsNullOrEmpty(placementId)) placementId = "default";
            if (!_settings.TryGetValue(placementId, out var state))
            {
                state = new AdPlacementState();
                _settings[placementId] = state;
            }
            return state;
        }

        public bool IsCooldownElapsed(string placementId, float cooldownSeconds)
        {
            var state = GetOrCreateState(placementId);
            if (state.LastAdWatchedAtUtcUnix <= 0)
            {
                return true;
            }

            double elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - state.LastAdWatchedAtUtcUnix;
            return elapsed >= cooldownSeconds;
        }

        public bool HasReachedMaxViews(string placementId, int maxViews)
        {
            var state = GetOrCreateState(placementId);
            return state.WatchCount >= maxViews;
        }

        public void RecordAdWatched(string placementId)
        {
            var state = GetOrCreateState(placementId);
            state.LastAdWatchedAtUtcUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            state.WatchCount++;
        }

        internal string ComputeNextAdAvailableUtcIso(string placementId, float cooldownSeconds)
        {
            var state = GetOrCreateState(placementId);
            if (state.LastAdWatchedAtUtcUnix <= 0 || IsCooldownElapsed(placementId, cooldownSeconds))
            {
                return string.Empty;
            }

            DateTime next = DateTimeOffset.FromUnixTimeSeconds(state.LastAdWatchedAtUtcUnix).UtcDateTime.AddSeconds(cooldownSeconds);
            return next.ToString("O");
        }
    }
}
