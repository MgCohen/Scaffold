using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.CloudModules
{
    /// <summary>
    /// Serves as the service contract for initializing and orchestrating all game modules.
    /// The main goal is to abstract the centralized loading and orchestration of multi-module setups.
    /// It is used during the game bootstrap phase to prepare necessary logical modules sequentially or in parallel.
    /// </summary>
    public interface IGameModulesService
    {
        public Awaitable InitializeModules(List<IGameModule> modules);

        public Awaitable FetchModuleData(params string[] fetchModuleKeys);
    }
}