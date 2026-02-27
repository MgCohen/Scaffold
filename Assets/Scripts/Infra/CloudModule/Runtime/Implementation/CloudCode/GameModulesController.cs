using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using GameModuleDTO.ModuleRequests;
using Scaffold.LifeCycle;
using UnityEngine;
using UnityEngine.Assertions;

namespace Scaffold.CloudModules
{
    /// <summary>
    /// Controls the lifecycle and initialization of all registered game modules using Cloud Code requests.
    /// The main goal is to orchestrate the backend fetching and localized data injection for each system.
    /// It is used primarily by the application's root controller to initialize systems sequentially.
    /// </summary>
    public class GameModulesController : IGameModulesService, IController
    {
        public GameModulesController(ICloudCodeService cloudCodeService, List<IGameModule> modules)
        {
            CloudCodeService = cloudCodeService;
            Modules = modules;
        }

        /// <summary>
        /// Gets the cloud code service dependency.
        /// The main goal is to access network RPC capabilities.
        /// It is used for backend operations directed by this controller.
        /// </summary>
        public ICloudCodeService CloudCodeService { get; }

        /// <summary>
        /// Gets the list of registered game modules.
        /// The main goal is to keep track of instantiated systems.
        /// It is used to iterate and lifecycle-manage the modules.
        /// </summary>
        public List<IGameModule> Modules { get; }

        /// <summary>
        /// Gets the authoritative game data state.
        /// The main goal is to cache the global state representing module configurations.
        /// It is used to feed sub-modules their respective startup data.
        /// </summary>
        public GameData GameData { get; protected set; }

        #region Implementation of IController
        /// <summary>
        /// Initializes the controller and all tracked modules.
        /// The main goal is to conform to the <see cref="IController"/> lifecycle.
        /// It is used during the core game entry point sequence.
        /// </summary>
        public async Awaitable Initialize()
        {
            await InitializeModules(Modules);
        }

        /// <summary>
        /// Disposes of the controller's resources.
        /// The main goal is to clean up state upon exit.
        /// It is used when the system shuts down or transitions contexts.
        /// </summary>
        public Awaitable Dispose()
        {
            throw new System.NotImplementedException();
        }
        #endregion

        /// <summary>
        /// Initializes a given list of modules concurrently.
        /// The main goal is to fetch their setup data and trigger their individual bootstrapping.
        /// It is used internally and directly via the service interface.
        /// </summary>
        public async Awaitable InitializeModules(List<IGameModule> modules)
        {
            var request = new InitializeGameModulesRequest(GameModuleAuthKey.guid);
            GameDataResponse response = await CloudCodeService.CallEndpointAsync(request);
            GameData = response.GameData;
            Assert.IsNotNull(GameData);
            IEnumerable<Task> initializeTasks = modules
                .Where(module => module != null)
                .Select(async module => await module.Initialize(GameData));
            await Task.WhenAll(initializeTasks);
        }

        /// <summary>
        /// Fetches refreshed data for specific module keys.
        /// The main goal is to ask the backend for partial state updates.
        /// It is used by gameplay mechanics to invalidate stale data after an action.
        /// </summary>
        public async Awaitable FetchModuleData(params string[] fetchModuleKeys)
        {
            var request = new GameDataRequest(GameModuleAuthKey.guid, fetchModuleKeys);
            GameDataResponse response = await CloudCodeService.CallEndpointAsync(request);
            if (!response.IsValid())
            {
                return;
            }

            foreach (IGameModuleData moduleData in response.GameData.modulesData)
            {
                IGameModule matchingModule = Modules.FirstOrDefault(m => m.DataModule?.GetType() == moduleData.GetType());
                matchingModule?.UpdateData(moduleData);
            }
        }
    }
}