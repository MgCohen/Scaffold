using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Common;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Ads
{
    /// <summary>
    /// Configuration data for the Ads module.
    /// </summary>
    public class AdsConfigData : IGameModuleData, IIsActive
    {
        public string Key { get { return GameDataExtensions.GetKey<AdsConfigData>(); } }

        [JsonProperty]
        private bool _isActive = true;

        [JsonIgnore]
        public bool IsActive => _isActive;

        public void SetActive(bool value)
        {
            _isActive = value;
        }

        [JsonProperty]
        private float _cooldown;

        /// <summary>Gets the cooldown time remaining until the next ad can be shown.</summary>
        [JsonIgnore]
        public float Cooldown { get { return _cooldown; } }

        public void SetCooldown(float value)
        {
            _cooldown = value;
        }
    }
}
