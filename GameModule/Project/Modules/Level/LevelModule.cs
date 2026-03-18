using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Level;
using GameModuleDTO.Modules.Common;
using GameModule.Modules.Gold;
using Microsoft.Extensions.Logging;
using System.Linq;
using Unity.Services.CloudCode.Core;

namespace GameModule.Modules.Level
{
    /// <summary>
    /// Module handling Level related logic and status.
    /// </summary>
    public class LevelModule : GameModule<LevelModuleData>
    {
        private readonly ILogger<LevelModule> _logger;
        private readonly GoldModule _goldModule;

        public LevelModule(ILogger<LevelModule> logger, GoldModule goldModule)
        {
            _logger = logger;
            _goldModule = goldModule;
        }

        public override bool Client => true;
        public override bool Server => false;

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData playerData, IGameState gameState, IRemoteConfig remoteConfig)
        {
            await remoteConfig.Get(context, new LevelConfigData());
            return await playerData.GetOrSet(context, new LevelModuleData());
        }

        [CloudCodeFunction(nameof(CompleteLevel))]
        public async Task CompleteLevel(IExecutionContext context, IPlayerData playerData, IRemoteConfig remoteConfig, int levelId)
        {
            LevelConfigData config = await remoteConfig.Get(context, new LevelConfigData());
            LevelModuleData data = await playerData.GetOrSet(context, new LevelModuleData());

            int currentLevel = 1;
            var completedLevels = data.Progress.Where(p => p.Status == ModuleStatus.Completed).ToList();
            if (completedLevels.Any())
            {
                int maxCompleted = completedLevels.Select(p => int.TryParse(p.Id, out int val) ? val : 0).Max();
                currentLevel = maxCompleted + 1;
            }

            if (levelId != currentLevel)
            {
                _logger.LogWarning("[LevelModule] Attempted to complete level {AttemptedLevel} but current level is {CurrentLevel}", levelId, currentLevel);
                return;
            }

            data.SetProgress(levelId.ToString(), ModuleStatus.Completed);
            playerData.AddToCache(data);

            await _goldServerModule.AddGoldToPlayer(context, playerData, context.PlayerId, config.Reward);
            _logger.LogInformation("[LevelModule] Level {LevelId} completed successfully for player {PlayerId}", levelId, context.PlayerId);
        }
    }
}
