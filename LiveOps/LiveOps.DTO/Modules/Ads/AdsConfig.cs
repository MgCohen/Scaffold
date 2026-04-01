using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Ads
{
    /// <summary>
    /// Remote configuration for the Ads module (merged from remote config service).
    /// </summary>
    public sealed class AdsConfig : IGameModuleData
    {
        /// <inheritdoc />
        public string Key => typeof(AdsConfig).Name;

        [JsonProperty]
        private float _cooldown;

        /// <summary>Gets the cooldown in seconds until the next ad can be shown.</summary>
        [JsonIgnore]
        public float Cooldown => _cooldown;

        /// <summary>Sets cooldown (remote config merge).</summary>
        public void SetCooldown(float value)
        {
            _cooldown = value;
        }
    }
}
