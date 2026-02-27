using GameModuleDTO.GameModule;
using UnityEngine;

namespace Scaffold.CloudModules
{
    /// <summary>
    /// Defines the baseline behavior for a generic game module in the application.
    /// The main goal is to enforce an initialization and data update structure for isolated systems.
    /// It is used by the GameModulesService to manage module lifecycles in an agnostic way.
    /// </summary>
    public interface IGameModule
    {
        /// <summary>
        /// Gets the module's associated data payload.
        /// The main goal is to expose state information linked to this module.
        /// It is used to quickly query the backend-configured or local cached state.
        /// </summary>
        public IGameModuleData DataModule { get; }

        /// <summary>
        /// Asynchronously initializes the game module using global game data.
        /// The main goal is to boot up internal systems once required data is present.
        /// It is used during app startup or module injection workflows.
        /// </summary>
        public Awaitable Initialize(GameData gameModules);

        /// <summary>
        /// Updates the internal data of the module with fresh information.
        /// The main goal is to push new state down to the localized systems safely.
        /// It is used to synchronize module data efficiently after network events.
        /// </summary>
        public void UpdateData(IGameModuleData gameModuleData);
    }
}