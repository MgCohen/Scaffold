using System.Threading.Tasks;
using LiveOps.Core.GameModule;
using LiveOps.Core.ModuleFetchData;
using LiveOps.Core.DTO.GameModule;
using LiveOps.Modules.DTO.Global;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.Global
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
