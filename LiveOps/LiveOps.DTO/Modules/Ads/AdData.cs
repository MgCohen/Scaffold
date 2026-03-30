using System;
using System.Globalization;
using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Ads
{
    /// <summary>
    /// Client-facing ads payload in <see cref="GameModuleDTO.GameModule.GameData"/> (built from persistence + config on the server).
    /// </summary>
    public sealed class AdData : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(AdData).Name;

        [JsonProperty]
        private float _cooldownSeconds;

        [JsonProperty]
        private string _nextAdAvailableUtc = string.Empty;

        /// <summary>Cooldown in seconds after a successful watch before another ad is allowed.</summary>
        [JsonIgnore]
        public float CooldownSeconds => _cooldownSeconds;

        /// <summary>ISO-8601 UTC instant when the next ad becomes available; empty when available immediately.</summary>
        [JsonIgnore]
        public string NextAdAvailableUtc => _nextAdAvailableUtc;

        /// <summary>Used by Newtonsoft when deserializing <c>GameData</c>.</summary>
        [JsonConstructor]
        private AdData()
        {
        }

        /// <summary>Build from persistence + config (server).</summary>
        public AdData(AdsPersistence persistence, AdsConfig config)
        {
            if (persistence == null)
            {
                throw new ArgumentNullException(nameof(persistence));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _cooldownSeconds = config.Cooldown;
            _nextAdAvailableUtc = persistence.ComputeNextAdAvailableUtcIso(config.Cooldown);
        }

        /// <summary>Whether an ad can be watched now (client-side check against server-supplied next-available time).</summary>
        public bool IsAdAvailable()
        {
            if (string.IsNullOrEmpty(_nextAdAvailableUtc))
            {
                return true;
            }

            if (DateTime.TryParse(_nextAdAvailableUtc, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime until))
            {
                return DateTime.UtcNow >= until;
            }

            return true;
        }

        /// <summary>Remaining cooldown before the next ad.</summary>
        public TimeSpan GetRemainingCooldown()
        {
            if (string.IsNullOrEmpty(_nextAdAvailableUtc))
            {
                return TimeSpan.Zero;
            }

            if (DateTime.TryParse(_nextAdAvailableUtc, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime until))
            {
                TimeSpan diff = until - DateTime.UtcNow;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }

            return TimeSpan.Zero;
        }
    }
}
