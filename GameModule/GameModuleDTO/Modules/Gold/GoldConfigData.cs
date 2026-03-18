using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Gold
{
    /// <summary>
    /// Configuration data for Gold settings, intended for Remote Config.
    /// </summary>
    public class GoldConfigData : IGameModuleData
    {
        /// <summary>Gets the resolved classification name for the component.</summary>
        public string Key { get { return GameDataExtensions.GetKey<GoldConfigData>(); } }

        [JsonProperty]
        private long _min;

        [JsonProperty]
        private long _max;

        /// <summary>Gets the minimum gold limit.</summary>
        [JsonIgnore]
        public long Min { get { return _min; } }

        /// <summary>Gets the maximum gold limit.</summary>
        [JsonIgnore]
        public long Max { get { return _max; } }

        public void SetLimits(long min, long max)
        {
            _min = min;
            _max = max;
        }
    }
}
