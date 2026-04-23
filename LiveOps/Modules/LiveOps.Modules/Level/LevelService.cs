using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.Core.GameModule;
using LiveOps.Core.ModuleFetchData;
using LiveOps.Core.Response;
using LiveOps.Core.DTO.GameModule;
using LiveOps.Core.DTO.ModuleRequest;
using LiveOps.Modules.DTO.ModuleRequests;
using LiveOps.Modules.DTO.Gold;
using LiveOps.Modules.DTO.Level;
using LiveOps.Modules.Gold;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.Level
{
    /// <summary>
    /// Cloud Code level module: persistence + remote config merged into <see cref="LevelGameData"/>.
    /// </summary>
    public class LevelService : GameModule<LevelGameData>
    {
        private readonly ILogger<LevelService> _logger;
        private readonly ModuleRequestHandler _moduleRequestHandler;
        private readonly GoldModule _goldModule;

        public LevelService(ILogger<LevelService> logger, ModuleRequestHandler moduleRequestHandler, GoldModule goldModule)
        {
            _logger = logger;
            _moduleRequestHandler = moduleRequestHandler;
            _goldModule = goldModule;
        }

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData Player, IGameState gameState, IRemoteConfig remoteConfig)
        {
            LevelPersistence persistence = await Player.GetOrSet(context, new LevelPersistence());
            LevelConfig config = await remoteConfig.Get(context, new LevelConfig());
            return new LevelGameData(persistence, config);
        }

        [CloudCodeFunction(nameof(CompleteLevelRequest))]
        public async Task<CompleteLevelResponse> CompleteLevel(IExecutionContext context, IPlayerData Player, IRemoteConfig remoteConfig, CompleteLevelRequest request)
        {
            LevelConfig config = await remoteConfig.Get(context, new LevelConfig());
            LevelPersistence persistence = await Player.GetOrSet(context, new LevelPersistence());

            IReadOnlyList<int> levels = config.Levels;
            int index = IndexOfLevelId(levels, request.LevelId);
            if (index < 0)
            {
                _logger.LogWarning("[LevelService] Attempted to complete level {AttemptedLevel} but it is not in the valid levels list", request.LevelId);
                return await _moduleRequestHandler.ResolveResponse(context, request, SnapshotResponse(false, persistence, config, null));
            }

            HashSet<int> completed = new HashSet<int>(persistence.CompletedLevelIds);
            if (completed.Contains(request.LevelId))
            {
                _logger.LogWarning("[LevelService] Level {LevelId} is already completed", request.LevelId);
                return await _moduleRequestHandler.ResolveResponse(context, request, SnapshotResponse(false, persistence, config, null));
            }

            if (index > 0)
            {
                int previousId = levels[index - 1];
                if (!completed.Contains(previousId))
                {
                    _logger.LogWarning("[LevelService] Previous level {PreviousId} is not completed for {AttemptedLevel}", previousId, request.LevelId);
                    return await _moduleRequestHandler.ResolveResponse(context, request, SnapshotResponse(false, persistence, config, null));
                }
            }

            persistence.AddCompletedLevel(request.LevelId);
            await Player.Set(context, persistence);

            int reward = config.RewardPerLevel;
            if (reward > 0)
            {
                await _goldModule.AddGoldToPlayer(context, Player, remoteConfig, reward, enqueueNestedResponse: true);
            }

            _logger.LogInformation("[LevelService] Level {LevelId} completed successfully for player {PlayerId}", request.LevelId, context.PlayerId);

            CompleteLevelResponse response = SnapshotResponse(true, persistence, config, request.LevelId);
            return await _moduleRequestHandler.ResolveResponse(context, request, response);
        }

        private static CompleteLevelResponse SnapshotResponse(bool succeeded, LevelPersistence persistence, LevelConfig config, int? completedLevelId)
        {
            LevelGameData data = new LevelGameData(persistence, config);
            return new CompleteLevelResponse(succeeded, data, completedLevelId);
        }

        private static int IndexOfLevelId(IReadOnlyList<int> levels, int levelId)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] == levelId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
