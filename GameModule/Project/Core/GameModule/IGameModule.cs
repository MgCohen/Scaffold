using GameModuleDTO.GameModule;
using GameModule.ModuleFetchData;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace GameModule.GameModule
{
    public interface IGameModule
    {
        public bool Client { get; }
        public bool Server { get; }
        public string Key { get; }

        public Task<IGameModuleData> Initialize(IExecutionContext context, PlayerData playerData, GameState gameState, RemoteConfig remoteConfig);
    }
}