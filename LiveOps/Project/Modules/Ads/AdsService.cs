using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModule.Modules.Gold;
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
        private readonly GoldModule _goldModule;

        public AdsService(ILogger<AdsService> logger, ModuleRequestHandler moduleRequestHandler, GoldModule goldModule)
        {
            _logger = logger;
            _moduleRequestHandler = moduleRequestHandler;
            _goldModule = goldModule;
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
            _logger.LogInformation("[WatchAdRequest] Starting for placement: {PlacementId}", request.PlacementId);
            AdsConfig config = await remoteConfig.Get(context, new AdsConfig());
            AdsPersistence persistence = await Player.GetOrSet(context, new AdsPersistence());

            string placementId = request.PlacementId ?? "default";
            AdPlacementConfig placementConfig = config.GetPlacement(placementId);

            if (persistence.HasReachedMaxViews(placementId, placementConfig.MaxViews))
            {
                _logger.LogWarning("[AdsService] Cannot watch ad. Max views reached for placement: {PlacementId}", placementId);
            }
            else if (persistence.IsCooldownElapsed(placementId, placementConfig.CooldownSeconds))
            {
                persistence.RecordAdWatched(placementId);
                Player.AddToCache(persistence);
                await GrantReward(context, Player, remoteConfig, placementConfig, placementId);
            }
            else
            {
                _logger.LogWarning("[AdsService] Cannot watch ad yet. On cooldown for placement: {PlacementId}", placementId);
            }

            AdData adData = new AdData(persistence, config);
            WatchAdResponse response = new WatchAdResponse(adData);
            return await _moduleRequestHandler.ResolveResponse(context, request, response);
        }

        private async Task GrantReward(IExecutionContext context, IPlayerData Player, IRemoteConfig remoteConfig, AdPlacementConfig placementConfig, string placementId)
        {
            if (placementConfig.RewardAmount <= 0 || string.IsNullOrEmpty(placementConfig.RewardType))
            {
                _logger.LogInformation("[AdsService] No reward configured for placement: {PlacementId}", placementId);
                return;
            }

            if (placementConfig.RewardType == _goldModule.Key)
            {
                await _goldModule.AddGoldToPlayer(context, Player, remoteConfig, placementConfig.RewardAmount, enqueueNestedResponse: true);
                _logger.LogInformation("[AdsService] Granted {Amount} gold for placement: {PlacementId}", placementConfig.RewardAmount, placementId);
            }
            else
            {
                _logger.LogWarning("[AdsService] Unknown RewardType '{RewardType}' for placement: {PlacementId}", placementConfig.RewardType, placementId);
            }
        }
    }
}
