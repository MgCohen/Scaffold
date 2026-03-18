using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Common
{
    /// <summary>
    /// Base class for module-specific configuration data.
    /// </summary>
    public abstract class BaseModuleConfigData : IGameModuleData
    {
        /// <summary>Gets the resolved classification name for the component.</summary>
        public abstract string Key { get; }

        [JsonProperty]
        private bool _isEnabled = true;

        /// <summary>Gets a value indicating whether the module is enabled in the configuration.</summary>
        [JsonIgnore]
        public bool IsEnabled => _isEnabled;

        public void SetEnabled(bool value)
        {
            _isEnabled = value;
        }
    }
}
