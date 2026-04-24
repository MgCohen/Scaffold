using LiveOps.DTO.Keys;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Gold
{
    [LiveOpsKey("GoldConfig")]
    public sealed class GoldConfig
    {
        [JsonProperty]
        private long _min;

        [JsonProperty]
        private long _max;

        [JsonIgnore]
        public long Min => _min;

        [JsonIgnore]
        public long Max => _max;

        public void SetLimits(long min, long max)
        {
            _min = min;
            _max = max;
        }
    }
}
