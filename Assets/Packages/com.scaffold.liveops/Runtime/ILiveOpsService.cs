using System.Threading;
using System.Threading.Tasks;
using LiveOps.DTO.GameModule;
using LiveOps.DTO.ModuleRequest;

namespace Scaffold.LiveOps
{
    public interface ILiveOpsService
    {
        T GetModuleData<T>() where T : class, IGameModuleData;

        Task<TResponse> CallAsync<TResponse>(ModuleRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : ModuleResponse;
    }
}
