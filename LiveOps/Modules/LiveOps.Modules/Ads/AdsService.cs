using System.Threading;
using System.Threading.Tasks;
using LiveOps.GameApi;
using LiveOps.GameModule;
using LiveOps.ModuleFetchData;
using LiveOps.Modules.Gold;
using LiveOps.DTO.GameModule;
using LiveOps.DTO.ModuleRequest;
using LiveOps.Modules.DTO.Ads;
using LiveOps.Modules.DTO.Gold;
using LiveOps.Modules.DTO.ModuleRequests;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
namespace LiveOps.Modules.Ads
{
    /// <summary>
    /// Cloud Code ads module: persistence + remote config merged into <see cref="AdData"/>.
    /// </summary>
    public class AdsService : GameModule<AdData>, IGameApiHandler<WatchAdRequest, WatchAdResponse>
    {
        private readonly ILogger<AdsService> _logger;
        private readonly GoldModule _goldModule;

        public AdsService(ILogger<AdsService> logger, GoldModule goldModule)
        {
            _logger = logger;
            _goldModule = goldModule;
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
                await GrantReward(context, Player, remoteConfig, placementConfig, placementId, session).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("[AdsService] Cannot watch ad yet. On cooldown for placement: {PlacementId}", placementId);
            }

            AdData adData = new AdData(persistence, config);
            return new WatchAdResponse(adData);
        }

        private async Task GrantReward(
            IExecutionContext context,
            IPlayerData player,
            IRemoteConfig remoteConfig,
            AdPlacementConfig placementConfig,
            string placementId,
            GameApiSession session)
        {
            if (placementConfig.RewardAmount <= 0 || string.IsNullOrEmpty(placementConfig.RewardType))
            {
                _logger.LogInformation("[AdsService] No reward configured for placement: {PlacementId}", placementId);
                return;
            }

            if (placementConfig.RewardType == _goldModule.Key)
            {
                await session.InvokeAsync<AddGoldRequest, GoldChangedResponse>(
                    new AddGoldRequest(placementConfig.RewardAmount)).ConfigureAwait(false);
                _logger.LogInformation("[AdsService] Granted {Amount} gold for placement: {PlacementId}", placementConfig.RewardAmount, placementId);
            }
            else
            {
                _logger.LogWarning("[AdsService] Unknown RewardType '{RewardType}' for placement: {PlacementId}", placementConfig.RewardType, placementId);
            }
        }
    }
}
