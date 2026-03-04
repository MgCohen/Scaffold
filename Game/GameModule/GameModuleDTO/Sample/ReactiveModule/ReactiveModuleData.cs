using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Sample.ReactiveModule
{
    /// <summary>
    /// Sample system modeling dual variable properties for reactive interfaces.
    /// </summary>
    public class ReactiveModuleData : IGameModuleData
    {
        /// <summary>Gets the explicitly mapped implementation key.</summary>
        public string Key { get { return GameDataExtensions.GetKey<ReactiveModuleData>(); } }

        [JsonProperty]
        private int _valueA;

        [JsonProperty]
        private int _valueB;

        /// <summary>
        /// Progresses property A recursively efficiently.
        /// </summary>
        /// <param name="increment">Numeric additive property.</param>
        public void IncreaseValueA(int increment)
        {
            _valueA += increment;
        }

        /// <summary>
        /// Progresses property B natively seamlessly.
        /// </summary>
        /// <param name="increment">Numeric additive parameter.</param>
        public void IncreaseValue(int increment)
        {
            _valueB += increment;
        }
    }
}
