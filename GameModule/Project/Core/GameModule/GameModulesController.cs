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
    /// <summary>
    /// Routes and executes the primary network calls across registered modules actively.
    /// </summary>
    public class GameModulesController
    {
        private readonly ILogger<GameModulesController> _logger;
        private readonly ModuleRequestHandler _moduleRequestHandler;
        private readonly IPlayerData _playerData;
        private readonly IRemoteConfig _remoteConfig;

        /// <summary>
        /// Initializes the central processing router correctly.
        /// </summary>
        /// <param name="logger">Core logging subsystem dependency.</param>
        /// <param name="moduleRequestHandler">Active handler execution instance natively.</param>
        /// <param name="playerData">The targeted executing player logic state component.</param>
        /// <param name="remoteConfig">The server remote configurations parameters element structure execution target parameter configuration object format.</param>
        public GameModulesController(
            ILogger<GameModulesController> logger,
            ModuleRequestHandler moduleRequestHandler,
            IPlayerData playerData,
            IRemoteConfig remoteConfig)
        {
            _logger = logger;
            _moduleRequestHandler = moduleRequestHandler;
            _playerData = playerData;
            _remoteConfig = remoteConfig;
        }

        /// <summary>
        /// Initializes all attached configurations actively correctly.
        /// </summary>
        /// <param name="context">The active processing runtime layer format object safely.</param>
        /// <param name="gameState">The specific logical component property mapping environment context state correctly.</param>
        /// <param name="request">The originating configuration startup command properties.</param>
        /// <param name="modules">The full list of available runtime features.</param>
        /// <returns>A mapped execution completion state object dynamically securely.</returns>
        [CloudCodeFunction(nameof(InitializeGameModulesRequest))]
        public async Task<GameDataResponse> InitializeModules(IExecutionContext context, IGameState gameState, InitializeGameModulesRequest request, IEnumerable<IGameModule> modules)
        {
            _logger.LogInformation("[InitializeModules] Starting");
            return await ProcessModulesSequentially(context, gameState, modules, request);
        }

        /// <summary>
        /// Fetches specified modules dynamically.
        /// </summary>
        /// <param name="context">Execution context.</param>
        /// <param name="gameState">Game state config.</param>
        /// <param name="request">Data request parameter.</param>
        /// <param name="modules">Module array data.</param>
        /// <returns>Data response dynamically.</returns>
        [CloudCodeFunction(nameof(GameDataRequest))]
        public async Task<GameDataResponse> GetGameModulesRequest(IExecutionContext context, IGameState gameState, GameDataRequest request, IEnumerable<IGameModule> modules)
        {
            _logger.LogInformation("[GetGameModulesRequest] Starting");
            return await ProcessModulesSequentially(context, gameState, modules, request, request.ModuleKeys);
        }

        private async Task<T> ProcessModulesSequentially<T>(
            IExecutionContext context,
            IGameState gameState,
            IEnumerable<IGameModule> modules,
            ModuleRequest<T> request,
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
            if (response == null)
            {
                throw new InvalidOperationException($"[ProcessModulesSequentially] Could not cast GameDataResponse to expected type '{typeof(T).Name}'.");
            }
            return await _moduleRequestHandler.ResolveResponse(context, request, response);
        }
    }
}