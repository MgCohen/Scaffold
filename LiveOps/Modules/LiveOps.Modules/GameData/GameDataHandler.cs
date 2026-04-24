using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiveOps.GameApi;
using LiveOps.GameModule;
using LiveOps.ModuleFetchData;
using LiveOps.DTO.GameModule;
using LiveOps.DTO.ModuleRequest;
using LiveOps.Modules.DTO.GameData;
using GameDataModel = LiveOps.DTO.GameModule.GameData;
using Microsoft.Extensions.Logging;

namespace LiveOps.Modules.GameData
{
    /// <summary>
    /// Builds <see cref="GameDataResponse"/> by initializing every registered <see cref="IGameModule" /> in parallel.
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

        public string[]? PlayerKeys() => ModulePrefetchKeys.UnionOrAll(_modules, static m => m.PlayerKeys());

        public string[]? ConfigKeys() => ModulePrefetchKeys.UnionOrAll(_modules, static m => m.ConfigKeys());

        public async Task<GameDataResponse> HandleAsync(GameApiSession session, GameDataRequest request)
        {
            GameDataModel gameData = new();
            IGameModule[] list = _modules?.Where(m => m != null).Cast<IGameModule>().ToArray() ?? Array.Empty<IGameModule>();
            if (list.Length == 0)
            {
                return new GameDataResponse(gameData, false, null);
            }

            var loaded = new ConcurrentQueue<IGameModuleData>();
            var errors = new ConcurrentQueue<GameDataModuleError>();
            await Task.WhenAll(
                list.Select(
                    m => RunModuleAsync(
                        m,
                        session,
                        loaded,
                        errors))).ConfigureAwait(false);

            foreach (IGameModuleData part in loaded)
            {
                gameData.AddModuleData(part);
            }

            List<GameDataModuleError> errList = errors.ToList();
            if (errList.Count > 0)
            {
                return new GameDataResponse(
                    gameData,
                    isPartial: true,
                    moduleLoadErrors: errList);
            }

            return new GameDataResponse(gameData, false, null);
        }

        private async Task RunModuleAsync(
            IGameModule gameModule,
            GameApiSession session,
            ConcurrentQueue<IGameModuleData> loaded,
            ConcurrentQueue<GameDataModuleError> errors)
        {
            try
            {
                IGameModuleData? moduleData = await gameModule
                    .InitializeAsync(session, default)
                    .ConfigureAwait(false);
                if (moduleData != null)
                {
                    loaded.Enqueue(moduleData);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[GameDataHandler] Error on module {ModuleKey}: {Message}", gameModule.Key, e.Message);
                errors.Enqueue(
                    new GameDataModuleError
                    {
                        ModuleKey = gameModule.Key,
                        Message = e.Message
                    });
            }
        }
    }
}
