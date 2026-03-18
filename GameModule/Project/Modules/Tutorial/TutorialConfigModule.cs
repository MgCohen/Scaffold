using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Tutorial;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Modules.Tutorial
{
    /// <summary>
    /// Module handling Tutorial configuration (remote config).
    /// </summary>
    public class TutorialConfigModule : GameModule<TutorialConfigData>
    {
        private readonly ILogger<TutorialConfigModule> _logger;

        public TutorialConfigModule(ILogger<TutorialConfigModule> logger)
        {
            _logger = logger;
        }

        public override bool Client => true;
        public override bool Server => true;

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData playerData, IGameState gameState, IRemoteConfig remoteConfig)
        {
            _logger.LogInformation("Initializing TutorialConfigModule");
            return await remoteConfig.Get(context, new TutorialConfigData());
        }
    }
}
