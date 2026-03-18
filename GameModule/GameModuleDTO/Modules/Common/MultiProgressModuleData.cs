using GameModuleDTO.GameModule;
using Newtonsoft.Json;

namespace GameModuleDTO.Modules.Common
{
    /// <summary>
    /// Base class for modules that hold multiple Progress items.
    /// </summary>
    public abstract class MultiProgressModuleData : IGameModuleData, IIsActive
    {
        /// <summary>Gets the resolved classification name for the component.</summary>
        public abstract string Key { get; }

        [JsonProperty]
        private bool _isActive;

        [JsonProperty]
        private List<ModuleProgress> _progress = new();

        /// <summary>Gets a value indicating whether the module is currently active.</summary>
        [JsonIgnore]
        public bool IsActive => _isActive;

        /// <summary>Gets the progress information for the module.</summary>
        [JsonIgnore]
        public List<ModuleProgress> Progress => _progress;

        public void SetActive(bool value)
        {
            _isActive = value;
        }

        public virtual void SetProgress(string id, ModuleStatus status, ModuleProgressState state = ModuleProgressState.Default)
        {
            ModuleProgress progress = _progress.FirstOrDefault(p => p.Id == id);
            if (progress == null)
            {
                progress = new ModuleProgress { Id = id };
                _progress.Add(progress);
            }
            progress.Status = status;
            progress.State = state;
        }
    }
}
