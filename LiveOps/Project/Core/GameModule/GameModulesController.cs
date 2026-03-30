using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        /// <param name="Player">The targeted executing player logic state component.</param>
        /// <param name="remoteConfig">The server remote configurations parameters element structure execution target parameter configuration object format.</param>
        public GameModulesController(
            ILogger<GameModulesController> logger,
            ModuleRequestHandler moduleRequestHandler,
            IPlayerData Player,
            IRemoteConfig remoteConfig)
        {
            _logger = logger;
            _moduleRequestHandler = moduleRequestHandler;
            _playerData = Player;
            _remoteConfig = remoteConfig;
        }

        /// <summary>
        /// Fetches specified modules dynamically.
        /// </summary>
        /// <param name="context">Execution context.</param>
        /// <param name="gameState">Game state config.</param>
        /// <param name="request">Data request parameter.</param>
        /// <param name="modules">All server <see cref="IGameModule"/> instances registered in <see cref="ModuleConfig"/> (cloud DI).</param>
        /// <returns>Data response dynamically.</returns>
        [CloudCodeFunction(nameof(GameDataRequest))]
        public async Task<GameDataResponse> GetGameModulesRequest(IExecutionContext context, IGameState gameState, GameDataRequest request, IEnumerable<IGameModule> modules)
        {
            _logger.LogInformation("[GetGameModulesRequest] Starting");
            return await ProcessModulesSequentially(context, gameState, modules, request, filterKeys: null);
        }

        private async Task<T> ProcessModulesSequentially<T>(
            IExecutionContext context,
            IGameState gameState,
            IEnumerable<IGameModule> modules,
            ModuleRequest<T> request,
            IReadOnlyCollection<string>? filterKeys = null) where T : ModuleResponse
        {
            // 1. Initialize the response type. 
            // If T is GameDataResponse, we pass the gameData object into it.
            GameData gameData = new GameData();

            IReadOnlyCollection<string>? keys = filterKeys;
            bool useKeyFilter = keys != null && keys.Count > 0;

            foreach (IGameModule gameModule in modules)
            {
                if (gameModule == null) continue;

                if (useKeyFilter && !keys!.Contains(gameModule.Key))
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