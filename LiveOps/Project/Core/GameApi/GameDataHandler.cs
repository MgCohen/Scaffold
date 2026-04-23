using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.ModuleRequests;
using Microsoft.Extensions.Logging;

namespace GameModule.GameApi
{
    /// <summary>
    /// Builds <see cref="GameDataResponse"/> by initializing every registered <see cref="IGameModule"/>.
    /// </summary>
    public sealed class GameDataHandler : IGameApiHandler<GameDataRequest, GameDataResponse>
    {
        private readonly ILogger<GameDataHandler> _logger;
        private readonly IEnumerable<IGameModule> _modules;

        public GameDataHandler(ILogger<GameDataHandler> logger, IEnumerable<IGameModule> modules)
        {
            _logger = logger;
            _modules = modules;
        }

        public string[]? PlayerKeys() => ModulePrefetchKeys.UnionOrAll(_modules, m => m.PlayerKeys());

        public string[]? ConfigKeys() => ModulePrefetchKeys.UnionOrAll(_modules, m => m.ConfigKeys());

        public async Task<GameDataResponse> HandleAsync(GameApiSession session, GameDataRequest request)
        {
            GameData gameData = new GameData();

            foreach (IGameModule gameModule in _modules)
            {
                if (gameModule == null)
                {
                    continue;
                }

                try
                {
                    IGameModuleData moduleData = await gameModule.Initialize(session.Context, session.Player, session.GameState, session.RemoteConfig).ConfigureAwait(false);
                    if (moduleData != null)
                    {
                        gameData.AddModuleData(moduleData);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "[GameDataHandler] Error on module {ModuleKey}: {Message}", gameModule.Key, e.Message);
                }
            }

            return new GameDataResponse(gameData);
        }
    }
}
