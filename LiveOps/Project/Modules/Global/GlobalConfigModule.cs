using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Global;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Modules.Global
{
    /// <summary>
    /// Handles server-side initialization for the Global configuration.
    /// </summary>
    public class GlobalConfigModule : GameModule<GlobalConfigData>
    {
        private readonly ILogger<GlobalConfigModule> _logger;

        public GlobalConfigModule(ILogger<GlobalConfigModule> logger)
        {
            _logger = logger;
        }

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData Player, IGameState gameState, IRemoteConfig remoteConfig)
        {
            _logger.LogInformation("Initializing GlobalConfigModule");
            return await remoteConfig.Get(context, new GlobalConfigData());
        }
    }
}
