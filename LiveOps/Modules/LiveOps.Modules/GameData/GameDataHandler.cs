using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.Core.GameApi;
using LiveOps.Core.GameModule;
using LiveOps.Core.ModuleFetchData;
using LiveOps.Core.DTO.GameModule;
using LiveOps.Core.DTO.ModuleRequest;
using LiveOps.Modules.DTO.GameData;
using GameDataModel = LiveOps.Core.DTO.GameModule.GameData;
using Microsoft.Extensions.Logging;

namespace LiveOps.Modules.GameData
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
            GameDataModel gameData = new GameDataModel();

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
