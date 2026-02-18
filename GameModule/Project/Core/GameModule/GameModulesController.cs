using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameModule.AccessKey;
using GameModuleDTO.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.Json;
using GameModuleDTO.ModuleRequests;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.GameModule
{
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

        [CloudCodeFunction(nameof(InitializeGameModulesRequest))]
        public async Task<string> InitializeModules(IExecutionContext context, GameState gameState, IEnumerable<IGameModule> modules, InitializeGameModulesRequest request)
        {
            logger.LogWarning($"GameModulesController.InitializeModules.Started");
            request.AssertModule();
            
            GameData gameData = new GameData();
            bool server = await context.GetValidAuth(gameState, request.AuthKey);
            foreach (IGameModule module in modules)
            {
                try
                {
                    if (module == null)
                    {
                        logger.LogWarning($"Null module found");
                        continue;
                    }

                    // Is Access Valid
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
            
            logger.LogWarning($"GameModulesController.InitializeModules.Finished");
            return request.GetResponse(new GameDataResponse(gameData));
        }
    }
}