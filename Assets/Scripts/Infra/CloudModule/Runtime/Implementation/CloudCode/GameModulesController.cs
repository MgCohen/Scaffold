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
        
        public ICloudCodeService CloudCodeService { get; }
        
        public List<IGameModule> Modules { get; }
        
        public GameData GameData { get; protected set; }
        
        #region Implementation of IController
        public async Awaitable Initialize()
        {
            await InitializeModules(Modules);
        }

        public Awaitable Dispose()
        {
            throw new System.NotImplementedException();
        }
        #endregion
        
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