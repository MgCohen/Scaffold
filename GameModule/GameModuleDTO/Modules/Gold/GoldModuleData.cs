using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Gold
{
    /// <summary>
    /// Data model for the Gold Client module (player-specific).
    /// </summary>
    public class GoldModuleData : IGameModuleData
    {
        /// <summary>Gets the resolved classification name for the component.</summary>
        public string Key { get { return GameDataExtensions.GetKey<GoldModuleData>(); } }

        [JsonProperty]
        private long _current;

        /// <summary>Gets the current gold amount.</summary>
        [JsonIgnore]
        public long Current { get { return _current; } }

        public void SetCurrent(long value)
        {
            _current = value;
        }
    }
}
