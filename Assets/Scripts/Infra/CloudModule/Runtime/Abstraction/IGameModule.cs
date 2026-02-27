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
        public IGameModuleData DataModule { get; }

        public Awaitable Initialize(GameData gameModules);

        public void UpdateData(IGameModuleData gameModuleData);
    }
}