using System;
using System.Collections.Generic;
using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Ads
{
    /// <summary>
    /// Player-persisted ads state: per-placement tracking of watches and times.
    /// </summary>
    public sealed class AdsPersistence : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(AdsPersistence).Name;

        [JsonProperty]
        private Dictionary<string, AdPlacementState> _settings = new Dictionary<string, AdPlacementState>();

        /// <summary>Gets the persisted states per placement.</summary>
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

        /// <summary>Whether another ad can be watched given the configured cooldown for a placement.</summary>
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

        /// <summary>Records a successful watch at the current UTC instant for a placement.</summary>
        public void RecordAdWatched(string placementId)
        {
            var state = GetOrCreateState(placementId);
            state.LastAdWatchedAtUtcUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            state.WatchCount++;
        }

        /// <summary>ISO UTC instant when the ad becomes available again, or empty if available now.</summary>
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
