using System;
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
    /// Module handling client-side Gold (player-specific data).
    /// </summary>
    public class GoldModule : GameModule<GoldModuleData>
    {
        private readonly ILogger<GoldModule> _logger;

        public GoldModule(ILogger<GoldModule> logger)
        {
            _logger = logger;
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

        public async Task AddGoldToPlayer(IExecutionContext context, IPlayerData playerData, long amount = 0)
        {
            _logger.LogInformation("[GoldModule] Rewarding player {PlayerId}", context.PlayerId);

            if (amount == 0)
            {
                GoldRewardModuleData serverData = await playerData.GetOrSet(context, new GoldRewardModuleData());
                amount = serverData.Reward;
            }

            GoldModuleData goldData = await playerData.GetOrSet(context, new GoldModuleData());
            goldData.SetCurrent(goldData.Current + amount);
            playerData.AddToCache(goldData);
            _logger.LogInformation("[GoldModule] Added {Amount} gold to player {PlayerId}. New total: {Total}", amount, context.PlayerId, goldData.Current);
        }
    }
}
