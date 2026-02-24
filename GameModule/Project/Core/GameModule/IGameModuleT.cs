using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using Unity.Services.CloudCode.Core;

public abstract class GameModuleT<T> : IGameModule where T : IGameModuleData
{
    public abstract bool Client { get; }

    public abstract bool Server { get; }

    public string Key
    {
        get
        {
            return GameDataExtensions.GetKey<T>();
        }
    }

    public Task<IGameModuleData> Initialize(IExecutionContext context, PlayerData playerData, GameState gameState, RemoteConfig remoteConfig)
    {
        throw new System.NotImplementedException();
    }
}