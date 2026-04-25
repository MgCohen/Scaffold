using System;
using System.Threading;
using System.Threading.Tasks;
using LiveOps.GameApi;
using LiveOps.GameModule;
using LiveOps.ModuleFetchData;
using LiveOps.DTO.GameModule;
using LiveOps.Modules.DTO.Gold;
using LiveOps.DTO.ModuleRequest;
using LiveOps.Modules.DTO.ModuleRequests;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.Gold
{

    public class GoldModule : GameModule<GoldGameData>, IGameApiHandler<AddGoldRequest, GoldChangedResponse>
    {
        private readonly ILogger<GoldModule> _logger;

        public GoldModule(ILogger<GoldModule> logger)
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
            _logger.LogInformation("Initializing GoldModule");

            GoldConfig config = await remoteConfig.Get(context, new GoldConfig());
            GoldPersistence persistence = await player.GetOrSet(context, new GoldPersistence());

            long clamped = Math.Clamp(persistence.Current, config.Min, config.Max);
            if (clamped != persistence.Current)
            {
                persistence.SetCurrent(clamped);
                await player.Set(context, persistence);
            }

            return new GoldGameData(persistence, config);
        }

        public Task<GoldChangedResponse> HandleAsync(GameApiSession session, AddGoldRequest request)
        {
            long amount = request?.Amount ?? 0;
            return AddGoldToPlayer(session.Context, session.Player, session.RemoteConfig, amount);
        }

        public async Task<GoldChangedResponse> AddGoldToPlayer(
            IExecutionContext context,
            IPlayerData player,
            IRemoteConfig remoteConfig,
            long amount)
        {
            _logger.LogInformation("[GoldModule] Rewarding player {PlayerId} with {Amount}", context.PlayerId, amount);

            if (amount == 0)
            {
                GoldPersistence currentPersistence = await player.GetOrSet(context, new GoldPersistence());
                return new GoldChangedResponse(currentPersistence.Current, 0);
            }

            GoldConfig config = await remoteConfig.Get(context, new GoldConfig());
            GoldPersistence goldPersistence = await player.GetOrSet(context, new GoldPersistence());
            long next = goldPersistence.Current + amount;
            long previous = goldPersistence.Current;
            _logger.LogInformation("[GoldModule] GoldConfig is from {Min} to {Max}", config.Min, config.Max);
            goldPersistence.SetCurrent(Math.Clamp(next, config.Min, config.Max));
            long actualDelta = goldPersistence.Current - previous;
            await player.Set(context, goldPersistence);
            _logger.LogInformation("[GoldModule] GoldPersistence is {Current} on delta {Delta}", goldPersistence.Current, actualDelta);

            GoldChangedResponse response = new GoldChangedResponse(goldPersistence.Current, actualDelta);
            _logger.LogInformation("[GoldModule] Added {Amount} gold to player {PlayerId}. New total: {Total}", amount, context.PlayerId, goldPersistence.Current);
            return response;
        }
    }
}
