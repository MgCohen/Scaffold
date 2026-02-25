using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Sample.SimpleModule;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Sample
{
    /// <summary>
    /// Example system showcasing bare minimum implementations.
    /// </summary>
    public class SimpleModule : GameModuleT<SimpleModuleData>
    {
        public SimpleModule(ILogger<SimpleModule> logger)
        {
            _logger = logger;
        }

        private readonly ILogger<SimpleModule> _logger;

        #region IGameModule implementation
        public override bool Client { get { return true; } }
        public override bool Server { get { return false; } }

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, PlayerData playerData, GameState gameState, RemoteConfig remoteConfig)
        {
            return await playerData.GetOrSet<SimpleModuleData>(context, Key, default);
        }
        #endregion
    }
}