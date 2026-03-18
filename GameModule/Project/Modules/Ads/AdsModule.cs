using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModule.Response;
using GameModuleDTO.GameModule;
using GameModuleDTO.ModuleRequests;
using GameModuleDTO.Modules.Ads;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Modules.Ads
{
    /// <summary>
    /// Module handling Ads related logic.
    /// </summary>
    public class AdsModule : GameModule<AdsModuleData>
    {
        private readonly ILogger<AdsModule> _logger;
        private readonly ModuleRequestHandler _moduleRequestHandler;

        public AdsModule(ILogger<AdsModule> logger, ModuleRequestHandler moduleRequestHandler)
        {
            _logger = logger;
            _moduleRequestHandler = moduleRequestHandler;
        }

        public override bool Client => true;
        public override bool Server => false;

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData playerData, IGameState gameState, IRemoteConfig remoteConfig)
        {
            return await playerData.GetOrSet(context, new AdsModuleData());
        }

        [CloudCodeFunction(nameof(WatchAdRequest))]
        public async Task<WatchAdResponse> WatchAd(IExecutionContext context, IPlayerData playerData, IRemoteConfig remoteConfig, WatchAdRequest request)
        {
            _logger.LogInformation("[WatchAdRequest] Starting");
            AdsConfigData config = await remoteConfig.Get(context, new AdsConfigData());
            AdsModuleData data = await playerData.GetOrSet(context, new AdsModuleData());

            if (data.IsAdAvailable())
            {
                data.SetNextAdAvailableTime(config.Cooldown);
                playerData.AddToCache(data);
                _logger.LogInformation("[AdsModule] Ad watched successfully. Next available at: {NextAdAvailableTime}", data.NextAdAvailableTime);
            }
            else
            {
                _logger.LogWarning("[AdsModule] Cannot watch ad yet. Remaining cooldown: {RemainingCooldown}", data.GetRemainingCooldown());
            }

            WatchAdResponse response = new WatchAdResponse(data);
            return await _moduleRequestHandler.ResolveResponse(context, request, response);
        }
    }
}
