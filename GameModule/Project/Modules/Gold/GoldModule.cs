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
    /// Module handling client-side Gold (player-specific data).
    /// </summary>
    public class GoldModule : GameModule<GoldModuleData>
    {
        private readonly ILogger<GoldModule> _logger;
        private readonly ModuleRequestHandler _handler;

        public GoldModule(ILogger<GoldModule> logger, ModuleRequestHandler handler)
        {
            _logger = logger;
            _handler = handler;
        }

        public override bool Client => true;
        public override bool Server => false;

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData playerData, IGameState gameState, IRemoteConfig remoteConfig)
        {
            _logger.LogInformation("Initializing GoldModule");

            GoldConfigData config = await remoteConfig.Get(context, new GoldConfigData());
            GoldModuleData data = await playerData.GetOrSet(context, new GoldModuleData());

            long clampedValue = Math.Clamp(data.Current, config.Min, config.Max);
            if (clampedValue != data.Current)
            {
                data.SetCurrent(clampedValue);
                await playerData.Set(context, data);
            }

            return data;
        }

        public async Task AddGoldToPlayer(IExecutionContext context, IPlayerData playerData, IRemoteConfig remoteConfig, long amount = 0)
        {
            _logger.LogInformation("[GoldModule] Rewarding player {PlayerId}", context.PlayerId);

            if (amount == 0)
            {
                GoldRewardModuleData configData = await remoteConfig.Get(context, new GoldRewardModuleData());
                amount = configData.Reward;
            }

            GoldModuleData goldData = await playerData.GetOrSet(context, new GoldModuleData());
            goldData.SetCurrent(goldData.Current + amount);
            playerData.AddToCache(goldData);

            _handler.AddResponse(new GoldResponse(amount));
            _logger.LogInformation("[GoldModule] Added {Amount} gold to player {PlayerId}. New total: {Total}", amount, context.PlayerId, goldData.Current);
        }
    }
}
