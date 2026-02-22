using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Sample.SimpleModuleData;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Sample
{
    public class SimpleModule : IGameModule
    {
        public SimpleModule(ILogger<SimpleModule> logger)
        {
            _logger = logger;
        }
                
        private readonly ILogger<SimpleModule> _logger;

        #region IGameModule implementation
        public bool Client { get { return true; } }
        public bool Server { get; }
        public string Key { get { return SimpleModuleData.StaticKey; } }
        
        public async Task<IGameModuleData> Initialize(IExecutionContext context, PlayerData playerData, GameState gameState, RemoteConfig remoteConfig)
        {
            return await playerData.GetOrSet<SimpleModuleData>(context, Key, default);
        }
        #endregion
    }
}