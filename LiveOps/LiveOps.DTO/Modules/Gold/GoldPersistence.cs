using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Gold
{
    /// <summary>
    /// Player-persisted gold balance (single scalar).
    /// </summary>
    public sealed class GoldPersistence : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(GoldPersistence).Name;

        [JsonProperty]
        private long _current;

        /// <summary>Current gold amount before clamping for client payload.</summary>
        [JsonIgnore]
        public long Current => _current;

        public void SetCurrent(long value)
        {
            _current = value;
        }
    }
}
