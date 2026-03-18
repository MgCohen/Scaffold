using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Gold;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Modules.Gold
{
    /// <summary>
    /// Module handling Gold configuration (remote config).
    /// </summary>
    public class GoldConfigModule : GameModule<GoldConfigData>
    {
        private readonly ILogger<GoldConfigModule> _logger;

        public GoldConfigModule(ILogger<GoldConfigModule> logger)
        {
            _logger = logger;
        }

        public override bool Client => true;
        public override bool Server => true;

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData playerData, IGameState gameState, IRemoteConfig remoteConfig)
        {
            _logger.LogInformation("Initializing GoldConfigModule");
            return await remoteConfig.Get(context, new GoldConfigData());
        }
    }
}
