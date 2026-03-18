using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Ads;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Modules.Ads
{
    /// <summary>
    /// Module handling Ads configuration (remote config).
    /// </summary>
    public class AdsConfigModule : GameModule<AdsConfigData>
    {
        private readonly ILogger<AdsConfigModule> _logger;

        public AdsConfigModule(ILogger<AdsConfigModule> logger)
        {
            _logger = logger;
        }

        public override bool Client => true;
        public override bool Server => true;

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData playerData, IGameState gameState, IRemoteConfig remoteConfig)
        {
            _logger.LogInformation("Initializing AdsConfigModule");
            return await remoteConfig.Get(context, new AdsConfigData());
        }
    }
}
