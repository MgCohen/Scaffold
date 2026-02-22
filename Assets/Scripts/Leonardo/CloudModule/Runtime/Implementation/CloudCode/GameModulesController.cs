using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using GameModuleDTO.ModuleRequests;
using Scaffold.LifeCycle.Shared;
using UnityEngine;
using UnityEngine.Assertions;

namespace Scaffold.CloudModules.Shared
{
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
            GameDataResponse response = await CloudCodeService.CallEndpointAsync(new InitializeGameModulesRequest(GameModuleAuthKey.guid));
            GameData = response.GameData;
            Assert.IsNotNull(GameData);
            IEnumerable<Task> initializeTasks = modules
                .Where(module => module != null)
                .Select(async module => await module.Initialize(GameData));
            await Task.WhenAll(initializeTasks);
        }
        
        public async Awaitable FetchModuleData(params string[] fetchModuleKeys)
        {
            GameDataResponse response = await CloudCodeService.CallEndpointAsync(new GameDataRequest(GameModuleAuthKey.guid, fetchModuleKeys));
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