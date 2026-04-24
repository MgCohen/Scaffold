using System;
using LiveOps.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Gold
{
    /// <summary>
    /// Aggregated gold payload returned in <see cref="LiveOps.DTO.GameModule.GameData"/> (persistence + config + reward defaults).
    /// </summary>
    public sealed class GoldGameData : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(GoldGameData).Name;

        [JsonProperty]
        private long _current;

        [JsonProperty]
        private long _min;

        [JsonProperty]
        private long _max;

        [JsonIgnore]
        public long Current => _current;

        [JsonIgnore]
        public long Min => _min;

        [JsonIgnore]
        public long Max => _max;

        /// <summary>Used by Newtonsoft when deserializing <c>GameData</c>.</summary>
        [JsonConstructor]
        private GoldGameData()
        {
        }

        /// <summary>Build from persistence + config + reward config (server).</summary>
        public GoldGameData(GoldPersistence persistence, GoldConfig config)
        {
            if (persistence == null)
            {
                throw new ArgumentNullException(nameof(persistence));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _min = config.Min;
            _max = config.Max;
            _current = Math.Clamp(persistence.Current, _min, _max);
        }
    }
}
