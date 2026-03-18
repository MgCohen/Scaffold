using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Tutorial;
using GameModuleDTO.Modules.Common;
using GameModule.Modules.Gold;
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

        public TutorialModule(ILogger<TutorialModule> logger, GoldModule goldModule)
        {
            _logger = logger;
            _goldModule = goldModule;
        }

        public override bool Client => true;
        public override bool Server => false;

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData playerData, IGameState gameState, IRemoteConfig remoteConfig)
        {
            await remoteConfig.Get(context, new TutorialConfigData());
            return await playerData.GetOrSet(context, new TutorialModuleData());
        }

        [CloudCodeFunction(nameof(CompleteTutorial))]
        public async Task CompleteTutorial(IExecutionContext context, IPlayerData playerData, IRemoteConfig remoteConfig, int tutorialId)
        {
            TutorialConfigData config = await remoteConfig.Get(context, new TutorialConfigData());
            TutorialModuleData data = await playerData.GetOrSet(context, new TutorialModuleData());

            int currentTutorialStep = 1;
            var completedSteps = data.Progress.Where(p => p.Status == ModuleStatus.Completed).ToList();
            if (completedSteps.Any())
            {
                int maxCompleted = completedSteps.Select(p => int.TryParse(p.Id, out int val) ? val : 0).Max();
                currentTutorialStep = maxCompleted + 1;
            }

            if (tutorialId != currentTutorialStep)
            {
                _logger.LogWarning("[TutorialModule] Attempted to complete tutorial step {AttemptedStep} but current step is {CurrentStep}", tutorialId, currentTutorialStep);
                return;
            }

            data.SetProgress(tutorialId.ToString(), ModuleStatus.Completed);
            playerData.AddToCache(data);

            await _goldServerModule.AddGoldToPlayer(context, playerData, context.PlayerId, config.Reward);
            _logger.LogInformation("[TutorialModule] Tutorial step {TutorialId} completed successfully for player {PlayerId}", tutorialId, context.PlayerId);
        }
    }
}
