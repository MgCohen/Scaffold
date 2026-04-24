using LiveOps.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Gold
{

    public sealed class GoldConfig : IGameModuleData
    {

        public string Key => typeof(GoldConfig).Name;

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
