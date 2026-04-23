using LiveOps.Core.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Gold
{
    /// <summary>
    /// Remote configuration for gold min/max bounds.
    /// </summary>
    public sealed class GoldConfig : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(GoldConfig).Name;

        [JsonProperty]
        private long _min;

        [JsonProperty]
        private long _max;

        /// <summary>Minimum gold balance.</summary>
        [JsonIgnore]
        public long Min => _min;

        /// <summary>Maximum gold balance.</summary>
        [JsonIgnore]
        public long Max => _max;

        public void SetLimits(long min, long max)
        {
            _min = min;
            _max = max;
        }
    }
}
