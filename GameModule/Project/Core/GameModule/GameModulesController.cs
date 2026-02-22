using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameModule.AccessKey;
using GameModuleDTO.GameModule;
using GameModule.ModuleFetchData;
using GameModule.Response;
using GameModuleDTO.ModuleRequests;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.GameModule
{
    public class GameModulesController
    {
        private readonly ILogger<GameModulesController> _logger;
        private readonly PlayerData _playerData;
        private readonly RemoteConfig _remoteConfig;

        public GameModulesController(
            ILogger<GameModulesController> logger, 
            PlayerData playerData, 
            RemoteConfig remoteConfig)
        {
            _logger = logger;
            _playerData = playerData;
            _remoteConfig = remoteConfig;
        }

        [CloudCodeFunction(nameof(InitializeGameModulesRequest))]
        public async Task<GameDataResponse> InitializeModules(IExecutionContext context, GameState gameState, IEnumerable<IGameModule> modules, InitializeGameModulesRequest request)
        {
            _logger.LogInformation("[InitializeModules] Starting");
            return await ProcessModulesSequentially(context, gameState, modules, request);
        }
        
        [CloudCodeFunction(nameof(GameDataRequest))]
        public async Task<GameDataResponse> GetGameModulesRequest(IExecutionContext context, GameState gameState, IEnumerable<IGameModule> modules, GameDataRequest request)
        {
            _logger.LogInformation("[GetGameModulesRequest] Starting");
            return await ProcessModulesSequentially(context, gameState, modules, request, request.ModuleKeys);
        }

        private async Task<T> ProcessModulesSequentially<T>(
            IExecutionContext context, 
            GameState gameState, 
            IEnumerable<IGameModule> modules, 
            ModuleRequestT<T> request,
            IReadOnlyCollection<string> filterKeys = null) where T : ModuleResponse
        {
            request.AssertModule();
    
            // 1. Initialize the response type. 
            // If T is GameDataResponse, we pass the gameData object into it.
            GameData gameData = new GameData();
            bool isServer = await context.GetValidAuth(gameState, request.AuthKey);

            foreach (IGameModule gameModule in modules)
            {
                if (gameModule == null) continue;

                bool hasAccess = isServer ? gameModule.Server : gameModule.Client;
                if (!hasAccess)
                {
                    continue;
                }

                if (filterKeys != null && !filterKeys.Contains(gameModule.Key))
                {
                    continue;
                }

                try
                {
                    IGameModuleData? moduleData = await gameModule.Initialize(context, _playerData, gameState, _remoteConfig);
                    if (moduleData != null)
                    {
                        gameData.AddModuleData(moduleData);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error on module {ModuleKey}: {Message}", gameModule.Key, e.Message);
                }
            }
            
            T? response = new GameDataResponse(gameData) as T;
            return await request.ResolveResponse(response, context, _playerData);
        }
    }
}