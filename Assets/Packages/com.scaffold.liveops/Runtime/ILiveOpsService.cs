using System.Threading;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using GameModuleDTO.ModuleRequests;

namespace Scaffold.LiveOps
{
    public interface ILiveOpsService
    {
        T GetModuleData<T>() where T : class, IGameModuleData;

        Task<TResponse> CallAsync<TResponse>(ModuleRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : ModuleResponse;
    }
}
