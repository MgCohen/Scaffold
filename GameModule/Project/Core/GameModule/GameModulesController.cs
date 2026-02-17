using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using GameModule.ModuleFetchData;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
using GameModuleDTO.Json;

namespace GameModule.GameModule
{
    using AccessKey = AccessKey.AccessKey;
    
    public class GameModulesController
    {
        public static ILogger loggerInstance;
        private ILogger<GameModulesController> logger;
        private PlayerData playerData;
        private RemoteConfig remoteConfig;

        public GameModulesController(ILogger<GameModulesController> logger, PlayerData playerData, RemoteConfig remoteConfig)
        {
            loggerInstance = logger;
            this.logger = logger;
            this.playerData = playerData;
            this.remoteConfig = remoteConfig;
        }

        [CloudCodeFunction("InitializeModules")]
        public async Task<string> InitializeModules(IExecutionContext context, GameState gameState, IEnumerable<IGameModule> modules, string serverKey)
        {
            logger.LogWarning($"GameModulesController.InitializeModules.Started");
            GameData gameData = new GameData();
            bool server = !string.IsNullOrEmpty(serverKey) && serverKey == await AccessKey.GetUnityAuth(gameState, context);
            foreach (IGameModule module in modules)
            {
                try
                {
                    if (module == null)
                    {
                        logger.LogWarning($"Null module found");
                        continue;
                    }

                    //isAccessInvalid
                    if (!(server && module.Server || !server && module.Client))
                    {
                        //TODO: enable logs based on environment
                        //logger.LogWarning($"Invalid module access - attempted specific access, server: {server} is trying to access module: {module.GetType().Name}");
                        continue;
                    }
                
                    IGameModuleData moduleData = await module.Initialize(context, playerData, gameState, remoteConfig);
                    gameData.AddModuleData(moduleData);
                }
                catch (Exception e)
                {
                    logger.LogError(e, e.Message);
                }
            }
            string json = gameData.ToUnityJson();
            logger.LogWarning($"GameModulesController.InitializeModules.Finished");
            //logger.LogWarning($"GameModulesController.InitializeModules.Finished: {json}");
            return json;
        }
    }
}