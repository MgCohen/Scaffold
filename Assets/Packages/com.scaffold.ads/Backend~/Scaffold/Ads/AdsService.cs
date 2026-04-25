using System.Threading;
using System.Threading.Tasks;
using LiveOps.GameApi;
using LiveOps.GameModule;
using LiveOps.ModuleFetchData;
using LiveOps.DTO.GameModule;
using LiveOps.DTO.ModuleRequest;
using LiveOps.Modules.DTO.Ads;
using LiveOps.Modules.DTO.ModuleRequests;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
namespace LiveOps.Modules.Ads
{

    public class AdsService : GameModule<AdData>, IGameApiHandler<WatchAdRequest, WatchAdResponse>
    {
        private readonly ILogger<AdsService> _logger;

        public AdsService(ILogger<AdsService> logger)
        {
            _logger = logger;
        }

        public override async Task<IGameModuleData> InitializeAsync(
            GameApiSession session,
            CancellationToken cancellationToken = default)
        {
            IExecutionContext context = session.Context;
            IPlayerData player = session.Player;
            IRemoteConfig remoteConfig = session.RemoteConfig;
            AdsPersistence persistence = await player.GetOrSet(context, new AdsPersistence());
            AdsConfig config = await remoteConfig.Get(context, new AdsConfig());
            return new AdData(persistence, config);
        }

        public async Task<WatchAdResponse> HandleAsync(GameApiSession session, WatchAdRequest request)
        {
            IExecutionContext context = session.Context;
            IPlayerData Player = session.Player;
            IRemoteConfig remoteConfig = session.RemoteConfig;

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
                await Player.Set(context, persistence);
                await GrantReward(placementConfig, placementId).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("[AdsService] Cannot watch ad yet. On cooldown for placement: {PlacementId}", placementId);
            }

            AdData adData = new AdData(persistence, config);
            return new WatchAdResponse(adData);
        }

        private Task GrantReward(AdPlacementConfig placementConfig, string placementId)
        {
            if (placementConfig.RewardAmount <= 0 || string.IsNullOrEmpty(placementConfig.RewardType))
            {
                _logger.LogInformation("[AdsService] No reward configured for placement: {PlacementId}", placementId);
                return Task.CompletedTask;
            }

            _logger.LogWarning(
                "[AdsService] RewardType '{RewardType}' / amount {Amount} is configured for placement {PlacementId}, but no built-in reward handler is registered. Add a game-specific module and wire rewards via IGameSetup.",
                placementConfig.RewardType,
                placementConfig.RewardAmount,
                placementId);
            return Task.CompletedTask;
        }
    }
}
