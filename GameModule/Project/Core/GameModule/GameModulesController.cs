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
        private readonly ModuleRequestHandler _moduleRequestHandler;

        public GameModulesController(
            ILogger<GameModulesController> logger, 
            PlayerData playerData, 
            RemoteConfig remoteConfig, 
            ModuleRequestHandler moduleRequestHandler)
        {
            _logger = logger;
            _playerData = playerData;
            _remoteConfig = remoteConfig;
            _moduleRequestHandler = moduleRequestHandler;
        }

        [CloudCodeFunction(nameof(InitializeGameModulesRequest))]
        public async Task<string> InitializeModules(IExecutionContext context, GameState gameState, IEnumerable<IGameModule> modules, InitializeGameModulesRequest request)
        {
            _logger.LogInformation("[GameModules] Starting full initialization.");
            return await ProcessModulesSequentially(context, gameState, modules, request);
        }

        [CloudCodeFunction(nameof(GameDataRequest))]
        public async Task<string> FetchModules(IExecutionContext context, GameState gameState, IEnumerable<IGameModule> modules, GameDataRequest request)
        {
            _logger.LogInformation("[GameModules] Fetching specific modules.");
            return await ProcessModulesSequentially(context, gameState, modules, request, request.ModuleKeys);
        }

        private async Task<string> ProcessModulesSequentially(
            IExecutionContext context, 
            GameState gameState, 
            IEnumerable<IGameModule> modules, 
            ModuleRequest request, 
            IReadOnlyCollection<string> filterKeys = null)
        {
            _moduleRequestHandler.SetCurrentRequest(request);
            request.AssertModule();
            
            GameData gameData = new GameData();
            bool isServer = await context.GetValidAuth(gameState, request.AuthKey);

            foreach (IGameModule gameModule in modules)
            {
                if (gameModule == null)
                {
                    continue;
                }

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
                    // Execução um por um, aguardando o término antes de ir para o próximo
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

            return request.GetResponse(new GameDataResponse(gameData));
        }
    }
}