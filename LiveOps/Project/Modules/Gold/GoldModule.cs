using System;
using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModule.Response;
using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Gold;
using GameModuleDTO.ModuleRequests;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Modules.Gold
{
    /// <summary>
    /// Cloud Code gold module: persistence + remote config merged into <see cref="GoldGameData"/>.
    /// </summary>
    public class GoldModule : GameModule<GoldGameData>
    {
        private readonly ILogger<GoldModule> _logger;
        private readonly ModuleRequestHandler _handler;

        public GoldModule(ILogger<GoldModule> logger, ModuleRequestHandler handler)
        {
            _logger = logger;
            _handler = handler;
        }

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData Player, IGameState gameState, IRemoteConfig remoteConfig)
        {
            _logger.LogInformation("Initializing GoldModule");

            GoldConfig config = await remoteConfig.Get(context, new GoldConfig());
            GoldPersistence persistence = await Player.GetOrSet(context, new GoldPersistence());

            long clamped = Math.Clamp(persistence.Current, config.Min, config.Max);
            if (clamped != persistence.Current)
            {
                persistence.SetCurrent(clamped);
                await Player.Set(context, persistence);
            }

            return new GoldGameData(persistence, config);
        }

        [CloudCodeFunction(nameof(AddGoldRequest))]
        public async Task<GoldChangedResponse> AddGold(IExecutionContext context, IPlayerData Player, IRemoteConfig remoteConfig, AddGoldRequest request)
        {
            long amount = request == null ? 0 : request.Amount;
            GoldChangedResponse response = await AddGoldToPlayer(context, Player, remoteConfig, amount, enqueueNestedResponse: false);
            return await _handler.ResolveResponse(context, request, response);
        }

        public async Task<GoldChangedResponse> AddGoldToPlayer(IExecutionContext context, IPlayerData Player, IRemoteConfig remoteConfig, long amount = 0, bool enqueueNestedResponse = true)
        {
            _logger.LogInformation("[GoldModule] Rewarding player {PlayerId} with {Amount}", context.PlayerId, amount);

            if (amount == 0)
            {
                GoldPersistence currentPersistence = await Player.GetOrSet(context, new GoldPersistence());
                return new GoldChangedResponse(currentPersistence.Current, 0);
            }

            GoldConfig config = await remoteConfig.Get(context, new GoldConfig());
            GoldPersistence goldPersistence = await Player.GetOrSet(context, new GoldPersistence());
            long next = goldPersistence.Current + amount;
            long previous = goldPersistence.Current;
            _logger.LogInformation("[GoldModule] GoldConfig is from {Min} to {Max}", config.Min, config.Max);
            goldPersistence.SetCurrent(Math.Clamp(next, config.Min, config.Max));
            long actualDelta = goldPersistence.Current - previous;
            await Player.Set(context, goldPersistence);
            _logger.LogInformation("[GoldModule] GoldPersistence is {Current} on delta {Delta}", goldPersistence.Current, actualDelta);

            GoldChangedResponse response = new GoldChangedResponse(goldPersistence.Current, actualDelta);
            if (enqueueNestedResponse)
            {
                _handler.AddResponse(response);
            }
            _logger.LogInformation("[GoldModule] Added {Amount} gold to player {PlayerId}. New total: {Total}", amount, context.PlayerId, goldPersistence.Current);
            return response;
        }
    }
}
