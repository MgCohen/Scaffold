using LiveOps.DTO.GameModule;
using Newtonsoft.Json;

namespace LiveOps.Modules.DTO.Gold
{

    public sealed class GoldPersistence : IGameModuleData
    {

        public string Key => typeof(GoldPersistence).Name;

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
