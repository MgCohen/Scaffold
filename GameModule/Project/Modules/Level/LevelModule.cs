using System.Collections.Generic;
using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Level;
using GameModuleDTO.Modules.Common;
using GameModule.Modules.Gold;
using GameModuleDTO.ModuleRequests;
using GameModule.Response;
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
        private readonly ModuleRequestHandler _moduleRequestHandler;

        public LevelModule(ILogger<LevelModule> logger, GoldModule goldModule, ModuleRequestHandler moduleRequestHandler)
        {
            _logger = logger;
            _goldModule = goldModule;
            _moduleRequestHandler = moduleRequestHandler;
        }

        public override bool Client => true;
        public override bool Server => false;

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData playerData, IGameState gameState, IRemoteConfig remoteConfig)
        {
            await remoteConfig.Get(context, new LevelConfigData());
            return await playerData.GetOrSet(context, new LevelModuleData());
        }

        [CloudCodeFunction(nameof(CompleteLevelRequest))]
        public async Task<CompleteLevelResponse> CompleteLevel(IExecutionContext context, IPlayerData playerData, IRemoteConfig remoteConfig, CompleteLevelRequest request)
        {
            LevelConfigData config = await remoteConfig.Get(context, new LevelConfigData());
            LevelModuleData data = await playerData.GetOrSet(context, new LevelModuleData());

            if (!config.Levels.Contains(request.LevelId))
            {
                _logger.LogWarning("[LevelModule] Attempted to complete level {AttemptedLevel} but it is not in the valid levels list", request.LevelId);
                return await _moduleRequestHandler.ResolveResponse(request, new CompleteLevelResponse(data), context, playerData);
            }

            int currentLevel = 1;
            List<ModuleProgress> completedLevels = data.Progress.Where(p => p.Status == ModuleStatus.Completed).ToList();
            if (completedLevels.Any())
            {
                int maxCompleted = completedLevels.Select(p => int.TryParse(p.Id, out int val) ? val : 0).Max();
                currentLevel = maxCompleted + 1;
            }

            if (request.LevelId != currentLevel)
            {
                _logger.LogWarning("[LevelModule] Attempted to complete level {AttemptedLevel} but current level is {CurrentLevel}", request.LevelId, currentLevel);
                return await _moduleRequestHandler.ResolveResponse(request, new CompleteLevelResponse(data), context, playerData);
            }

            data.SetProgress(request.LevelId.ToString(), ModuleStatus.Completed);
            playerData.AddToCache(data);

            await _goldModule.AddGoldToPlayer(context, playerData, remoteConfig, config.Reward);
            _logger.LogInformation("[LevelModule] Level {LevelId} completed successfully for player {PlayerId}", request.LevelId, context.PlayerId);

            CompleteLevelResponse response = new CompleteLevelResponse(data);
            return await _moduleRequestHandler.ResolveResponse(request, response, context, playerData);
        }
    }
}
