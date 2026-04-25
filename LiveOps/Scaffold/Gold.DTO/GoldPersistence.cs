using LiveOps.DTO.Keys;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Gold
{
    [LiveOpsKey("GoldPersistence")]
    public sealed class GoldPersistence
    {
        [JsonProperty]
        private long _current;

        [JsonIgnore]
        public long Current => _current;

        public void SetCurrent(long value)
        {
            _current = value;
        }
    }
}
