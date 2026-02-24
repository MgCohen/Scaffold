using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Sample.ReactiveModule
{
    public class ReactiveModuleData : IGameModuleData
    {
        public string Key { get { return GameDataExtensions.GetKey<ReactiveModuleData>(); } }

        [JsonProperty]
        private int valueA;
        [JsonProperty]
        private int valueB;

        public void IncreaseValueA(int increment)
        {
            valueA += increment;
        }

        public void IncreaseValue(int increment)
        {
            valueB += increment;
        }
    }
}