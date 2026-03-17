using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using GameModuleDTO.ModuleRequests;
using Scaffold.CloudGateway;
using Scaffold.LifeCycle;
using UnityEngine.Assertions;

namespace Scaffold.GameModules
{
    /// <summary>
    /// Controls the lifecycle and initialization of all registered game modules using Cloud Code requests.
    /// The main goal is to orchestrate the backend fetching and localized data injection for each system.
    /// It is used primarily by the application's root controller to initialize systems sequentially.
    /// </summary>
    public class GameModulesController : IGameModulesService, IController
    {
        public GameModulesController(ICloudGatewayAuthKey cloudGatewayAuthKey, ICloudService cloudService, List<IGameModule> modules)
        {
            CloudGatewayAuthKey =  cloudGatewayAuthKey;
            CloudService = cloudService;
            Modules = modules;
        }

        public ICloudGatewayAuthKey CloudGatewayAuthKey { get; }
        
        public ICloudService CloudService { get; }

        public List<IGameModule> Modules { get; }

        public GameData GameData { get; protected set; }

        #region Implementation of IController
        public async Task Initialize()
        {
            await InitializeModules(Modules);
        }

        public Task Dispose()
        {
            throw new System.NotImplementedException();
        }
        #endregion

        public async Task InitializeModules(List<IGameModule> modules)
        {
            InitializeGameModulesRequest request = new InitializeGameModulesRequest(CloudGatewayAuthKey.Guid);
            GameDataResponse response = await CloudService.CallEndpointAsync(request);
            GameData = response.GameData;
            Assert.IsNotNull(GameData);
            IEnumerable<Task> initializeTasks = modules
                .Where(module => module != null)
                .Select(async module => await module.Initialize(GameData));
            await Task.WhenAll(initializeTasks);
        }

        public async Task FetchModuleData(params string[] fetchModuleKeys)
        {
            GameDataRequest request = new GameDataRequest(CloudGatewayAuthKey.Guid, fetchModuleKeys);
            GameDataResponse response = await CloudService.CallEndpointAsync(request);
            if (!response.IsValid())
            {
                return;
            }

            foreach (IGameModuleData moduleData in response.GameData.ModulesData)
            {
                IGameModule matchingModule = Modules.FirstOrDefault(m => m.DataModule?.GetType() == moduleData.GetType());
                matchingModule?.UpdateData(moduleData);
            }
        }
    }
}