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
    /// Cloud Code ads module: persistence + remote config merged into <see cref="AdData"/>.
    /// </summary>
    public class AdsService : GameModule<AdData>
    {
        private readonly ILogger<AdsService> _logger;
        private readonly ModuleRequestHandler _moduleRequestHandler;

        public AdsService(ILogger<AdsService> logger, ModuleRequestHandler moduleRequestHandler)
        {
            _logger = logger;
            _moduleRequestHandler = moduleRequestHandler;
        }

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData Player, IGameState gameState, IRemoteConfig remoteConfig)
        {
            AdsPersistence persistence = await Player.GetOrSet(context, new AdsPersistence());
            AdsConfig config = await remoteConfig.Get(context, new AdsConfig());
            return new AdData(persistence, config);
        }

        [CloudCodeFunction(nameof(WatchAdRequest))]
        public async Task<WatchAdResponse> WatchAd(IExecutionContext context, IPlayerData Player, IRemoteConfig remoteConfig, WatchAdRequest request)
        {
            _logger.LogInformation("[WatchAdRequest] Starting");
            AdsConfig config = await remoteConfig.Get(context, new AdsConfig());
            AdsPersistence persistence = await Player.GetOrSet(context, new AdsPersistence());

            if (persistence.IsCooldownElapsed(config.Cooldown))
            {
                persistence.RecordAdWatched();
                Player.AddToCache(persistence);
                _logger.LogInformation("[AdsService] Ad watched successfully.");
            }
            else
            {
                _logger.LogWarning("[AdsService] Cannot watch ad yet. Remaining cooldown: {RemainingCooldown}", new AdData(persistence, config).GetRemainingCooldown());
            }

            AdData adData = new AdData(persistence, config);
            WatchAdResponse response = new WatchAdResponse(adData);
            return await _moduleRequestHandler.ResolveResponse(context, request, response);
        }
    }
}
