using System.Collections.Generic;
using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Tutorial;
using GameModuleDTO.Modules.Common;
using GameModule.Modules.Gold;
using GameModuleDTO.ModuleRequests;
using GameModule.Response;
using Microsoft.Extensions.Logging;
using System.Linq;
using Unity.Services.CloudCode.Core;

namespace GameModule.Modules.Tutorial
{
    /// <summary>
    /// Module handling Tutorial related logic and status.
    /// </summary>
    public class TutorialModule : GameModule<TutorialModuleData>
    {
        private readonly ILogger<TutorialModule> _logger;
        private readonly GoldModule _goldModule;
        private readonly ModuleRequestHandler _moduleRequestHandler;

        public TutorialModule(ILogger<TutorialModule> logger, GoldModule goldModule, ModuleRequestHandler moduleRequestHandler)
        {
            _logger = logger;
            _goldModule = goldModule;
            _moduleRequestHandler = moduleRequestHandler;
        }

        public override bool Client => true;
        public override bool Server => false;

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData playerData, IGameState gameState, IRemoteConfig remoteConfig)
        {
            await remoteConfig.Get(context, new TutorialConfigData());
            return await playerData.GetOrSet(context, new TutorialModuleData());
        }

        [CloudCodeFunction(nameof(CompleteTutorialRequest))]
        public async Task<CompleteTutorialResponse> CompleteTutorial(IExecutionContext context, IPlayerData playerData, IRemoteConfig remoteConfig, CompleteTutorialRequest request)
        {
            TutorialConfigData config = await remoteConfig.Get(context, new TutorialConfigData());
            TutorialModuleData data = await playerData.GetOrSet(context, new TutorialModuleData());

            if (!config.Tutorials.Contains(request.TutorialId))
            {
                _logger.LogWarning("[TutorialModule] Attempted to complete tutorial step {AttemptedStep} but it is not in the valid tutorials list", request.TutorialId);
                return await _moduleRequestHandler.ResolveResponse(request, new CompleteTutorialResponse(data), context, playerData);
            }

            int currentTutorialStep = 1;
            List<ModuleProgress> completedSteps = data.Progress.Where(p => p.Status == ModuleStatus.Completed).ToList();
            if (completedSteps.Any())
            {
                int maxCompleted = completedSteps.Select(p => int.TryParse(p.Id, out int val) ? val : 0).Max();
                currentTutorialStep = maxCompleted + 1;
            }

            if (request.TutorialId != currentTutorialStep)
            {
                _logger.LogWarning("[TutorialModule] Attempted to complete tutorial step {AttemptedStep} but current step is {CurrentStep}", request.TutorialId, currentTutorialStep);
                return await _moduleRequestHandler.ResolveResponse(request, new CompleteTutorialResponse(data), context, playerData);
            }

            data.SetProgress(request.TutorialId.ToString(), ModuleStatus.Completed);
            playerData.AddToCache(data);

            await _goldModule.AddGoldToPlayer(context, playerData, remoteConfig, config.Reward);
            _logger.LogInformation("[TutorialModule] Tutorial step {TutorialId} completed successfully for player {PlayerId}", request.TutorialId, context.PlayerId);

            CompleteTutorialResponse response = new CompleteTutorialResponse(data);
            return await _moduleRequestHandler.ResolveResponse(request, response, context, playerData);
        }
    }
}
