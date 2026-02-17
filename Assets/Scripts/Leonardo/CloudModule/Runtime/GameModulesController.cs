using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using Scaffold.LifeCycle.Shared;
using UnityEngine;
using UnityEngine.Assertions;
using VContainer;

namespace Scaffold.CloudModules.Shared
{
    public class GameModulesController : IController
    {
        [Inject]
        [SerializeField]
        private GameModuleBindings bindings;
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
            gameData = await bindings.InitializeModules();
            Assert.IsNotNull(gameData);
            // Convert Awaitables to Tasks to use WhenAll
            IEnumerable<Task> initializeTasks = bindings.Modules
                .Where(module => module != null)
                .Select(async module => await module.Initialize(gameData));
            await Task.WhenAll(initializeTasks);
        }
    }
}