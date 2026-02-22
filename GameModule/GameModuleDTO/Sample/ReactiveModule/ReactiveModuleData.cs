using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Sample.ReactiveModule
{
    public class ReactiveModuleData : IGameModuleData
    {
        public static string StaticKey { get { return nameof(ReactiveModuleData); } }
        public string Key { get { return StaticKey; } }

        [JsonProperty]
        private int value;

        public void IncreaseValue(int increment)
        {
            value += increment;
        }
    }
}