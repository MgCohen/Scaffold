using System.Collections.Generic;
using System.Threading.Tasks;
using Scaffold.LifeCycle;

namespace Scaffold.GameModules
{
    /// <summary>
    /// Serves as the service contract for initializing and orchestrating all game modules.
    /// The main goal is to abstract the centralized loading and orchestration of multi-module setups.
    /// It is used during the game bootstrap phase to prepare necessary logical modules sequentially or in parallel.
    /// </summary>
    public interface IGameModulesService : IController
    {
        public Task InitializeModules(IEnumerable<IGameModule> modules);

        public Task FetchModuleData(params string[] fetchModuleKeys);
    }
}