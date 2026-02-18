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
    public class GameModulesController : IController
    {
        [SerializeField]
        private ICloudCodeService _services;
        [SerializeField]
        private List<IGameModule> _modules;

        public GameModulesController(ICloudCodeService services, List<IGameModule> modules)
        {
            _services = services;
            _modules = modules;
        }
        
        [SerializeField]
        private GameData gameData;
        
        #region IController
        public async Awaitable Initialize()
        {
            await InitializeModules();
        }

        public Awaitable Dispose()
        {
            throw new System.NotImplementedException();
        }
        #endregion
        
        public async Awaitable InitializeModules()
        {
            GameDataResponse response = await _services.CallEndpointAsync(new InitializeGameModulesRequest(GameModuleAuthKey.guid));
            gameData = response.GameData;
            Assert.IsNotNull(gameData);
            // Convert Awaitables to Tasks to use WhenAll
            IEnumerable<Task> initializeTasks = _modules
                .Where(module => module != null)
                .Select(async module => await module.Initialize(gameData));
            await Task.WhenAll(initializeTasks);
        }
    }
}