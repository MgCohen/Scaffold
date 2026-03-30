using System;
using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Ads
{
    /// <summary>
    /// Player-persisted ads state: last successful watch time only (cooldown duration comes from remote config).
    /// </summary>
    public sealed class AdsPersistence : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(AdsPersistence).Name;

        [JsonProperty]
        private long _lastAdWatchedAtUtcUnix;

        /// <summary>Unix seconds (UTC) of the last successful ad watch; <c>0</c> means never watched.</summary>
        [JsonIgnore]
        public long LastAdWatchedAtUtcUnix => _lastAdWatchedAtUtcUnix;

        /// <summary>Whether another ad can be watched given the configured cooldown (server clock).</summary>
        public bool IsCooldownElapsed(float cooldownSeconds)
        {
            if (_lastAdWatchedAtUtcUnix <= 0)
            {
                return true;
            }

            double elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _lastAdWatchedAtUtcUnix;
            return elapsed >= cooldownSeconds;
        }

        /// <summary>Records a successful watch at the current UTC instant.</summary>
        public void RecordAdWatched()
        {
            _lastAdWatchedAtUtcUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>ISO UTC instant when the ad becomes available again, or empty if available now.</summary>
        internal string ComputeNextAdAvailableUtcIso(float cooldownSeconds)
        {
            if (_lastAdWatchedAtUtcUnix <= 0 || IsCooldownElapsed(cooldownSeconds))
            {
                return string.Empty;
            }

            DateTime next = DateTimeOffset.FromUnixTimeSeconds(_lastAdWatchedAtUtcUnix).UtcDateTime.AddSeconds(cooldownSeconds);
            return next.ToString("O");
        }
    }
}
