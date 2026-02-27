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
        /// <summary>
        /// Executes the sequential or parallel initialization of a collection of game modules.
        /// The main goal is to prepare all referenced subsystems for active duty.
        /// It is used as the top-level orchestration step when booting the client architecture.
        /// </summary>
        public Awaitable InitializeModules(List<IGameModule> modules);

        /// <summary>
        /// Re-fetches remote module data based on specified module keys.
        /// The main goal is to update subset module state dynamically.
        /// It is used to pull the latest state after specific interactions or refresh triggers.
        /// </summary>
        public Awaitable FetchModuleData(params string[] fetchModuleKeys);
    }
}