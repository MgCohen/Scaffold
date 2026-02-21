using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Sample.ReactiveModule;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Sample.ReactiveModule
{
    public class ReactiveModule: IGameModule
    {
        public ReactiveModule(ILogger<ReactiveModule> logger)
        {
            _logger = logger;
        }
                
        private readonly ILogger<ReactiveModule> _logger;
        
        #region IGameModule implementation
        public bool Client { get { return true; } }
        public bool Server { get; }
        public string Key { get { return ReactiveModuleData.StaticKey; } }
        
        public async Task<IGameModuleData> Initialize(IExecutionContext context, PlayerData playerData, GameState gameState, RemoteConfig remoteConfig)
        {
            return await playerData.GetOrSet<ReactiveModuleData>(context, Key, default);
        }
        #endregion
    }
}