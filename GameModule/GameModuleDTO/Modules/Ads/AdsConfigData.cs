using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Common;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Ads
{
    /// <summary>
    /// Configuration data for the Ads module.
    /// </summary>
    public class AdsConfigData : BaseModuleConfigData
    {
        public override string Key { get { return GameDataExtensions.GetKey<AdsConfigData>(); } }

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
