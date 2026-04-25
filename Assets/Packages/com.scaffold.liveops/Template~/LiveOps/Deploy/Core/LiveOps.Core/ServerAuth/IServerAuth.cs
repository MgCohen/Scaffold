using System.Threading;
using System.Threading.Tasks;
using LiveOps.ModuleFetchData;
using Unity.Services.CloudCode.Core;

namespace LiveOps.ServerAuth
{

    public interface IServerAuth
    {

        Task<bool> IsValidForServerAccessAsync(
            IGameState gameState,
            IExecutionContext context,
            string guid,
            CancellationToken cancellationToken = default);
    }
}
