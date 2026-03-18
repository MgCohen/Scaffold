using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Level;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Modules.Level
{
    /// <summary>
    /// Module handling Level configuration (remote config).
    /// </summary>
    public class LevelConfigModule : GameModule<LevelConfigData>
    {
        private readonly ILogger<LevelConfigModule> _logger;

        public LevelConfigModule(ILogger<LevelConfigModule> logger)
        {
            _logger = logger;
        }

        public override bool Client => true;
        public override bool Server => true;

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData playerData, IGameState gameState, IRemoteConfig remoteConfig)
        {
            _logger.LogInformation("Initializing LevelConfigModule");
            return await remoteConfig.Get(context, new LevelConfigData());
        }
    }
}
