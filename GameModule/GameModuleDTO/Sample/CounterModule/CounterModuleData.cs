using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Sample.CounterModule
{
    public class CounterModuleData : IGameModuleData
    {
        public string Key { get { return GameDataExtensions.GetKey<CounterModuleData>(); } }

        [JsonProperty]
        private int value;
        [JsonIgnore]
        public int Value { get { return value; } }

        public void IncreaseValue(int increment)
        {
            value += increment;
        }
    }
}