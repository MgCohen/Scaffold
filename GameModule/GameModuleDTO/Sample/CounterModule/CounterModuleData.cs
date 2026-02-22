using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Sample.CounterModule
{
    public class CounterModuleData : IGameModuleData
    {
        public static string StaticKey { get { return nameof(CounterModuleData); } }
        public string Key { get { return StaticKey; } }

        [JsonProperty]
        private int value;

        public void IncreaseValue(int increment)
        {
            value += increment;
        }
    }
}