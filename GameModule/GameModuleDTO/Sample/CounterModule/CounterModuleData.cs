using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Sample.CounterModule
{
    /// <summary>
    /// Sample data model representing a basic network counter configuration.
    /// </summary>
    public class CounterModuleData : IGameModuleData
    {
        /// <summary>Gets the resolved classification name for the component.</summary>
        public string Key { get { return GameDataExtensions.GetKey<CounterModuleData>(); } }

        [JsonProperty]
        private int _value;

        /// <summary>Gets the tracking integer value explicitly.</summary>
        [JsonIgnore]
        public int Value { get { return _value; } }

        /// <summary>
        /// Steps the internal integer value predictably.
        /// </summary>
        /// <param name="increment">The delta modifier integer.</param>
        public void IncreaseValue(int increment)
        {
            _value += increment;
        }
    }
}