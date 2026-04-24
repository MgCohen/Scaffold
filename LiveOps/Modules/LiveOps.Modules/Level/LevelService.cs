using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LiveOps.GameApi;
using LiveOps.GameModule;
using LiveOps.ModuleFetchData;
using LiveOps.DTO.GameModule;
using LiveOps.DTO.ModuleRequest;
using LiveOps.Modules.DTO.Gold;
using LiveOps.Modules.DTO.ModuleRequests;
using LiveOps.Modules.DTO.Level;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
namespace LiveOps.Modules.Level
{
    /// <summary>
    /// Cloud Code level module: persistence + remote config merged into <see cref="LevelGameData"/>.
    /// </summary>
    public class LevelService : GameModule<LevelGameData>, IGameApiHandler<CompleteLevelRequest, CompleteLevelResponse>
    {
        private readonly ILogger<LevelService> _logger;

        public LevelService(ILogger<LevelService> logger)
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
            LevelPersistence persistence = await player.GetOrSet(context, new LevelPersistence());
            LevelConfig config = await remoteConfig.Get(context, new LevelConfig());
            return new LevelGameData(persistence, config);
        }

        public async Task<CompleteLevelResponse> HandleAsync(GameApiSession session, CompleteLevelRequest request)
        {
            IExecutionContext context = session.Context;
            IPlayerData Player = session.Player;
            IRemoteConfig remoteConfig = session.RemoteConfig;

            LevelConfig config = await remoteConfig.Get(context, new LevelConfig());
            LevelPersistence persistence = await Player.GetOrSet(context, new LevelPersistence());

            IReadOnlyList<int> levels = config.Levels;
            int index = IndexOfLevelId(levels, request.LevelId);
            if (index < 0)
            {
                _logger.LogWarning("[LevelService] Attempted to complete level {AttemptedLevel} but it is not in the valid levels list", request.LevelId);
                return SnapshotResponse(false, persistence, config, null);
            }

            HashSet<int> completed = new HashSet<int>(persistence.CompletedLevelIds);
            if (completed.Contains(request.LevelId))
            {
                _logger.LogWarning("[LevelService] Level {LevelId} is already completed", request.LevelId);
                return SnapshotResponse(false, persistence, config, null);
            }

            if (index > 0)
            {
                int previousId = levels[index - 1];
                if (!completed.Contains(previousId))
                {
                    _logger.LogWarning("[LevelService] Previous level {PreviousId} is not completed for {AttemptedLevel}", previousId, request.LevelId);
                    return SnapshotResponse(false, persistence, config, null);
                }
            }

            persistence.AddCompletedLevel(request.LevelId);
            await Player.Set(context, persistence);

            int reward = config.RewardPerLevel;
            if (reward > 0)
            {
                await session.InvokeAsync<AddGoldRequest, GoldChangedResponse>(new AddGoldRequest(reward)).ConfigureAwait(false);
            }

            _logger.LogInformation("[LevelService] Level {LevelId} completed successfully for player {PlayerId}", request.LevelId, context.PlayerId);

            return SnapshotResponse(true, persistence, config, request.LevelId);
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
