using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.Scope.Contracts
{
    public interface IAsyncInitializable
    {
        Task InitializeAsync(CancellationToken cancellationToken);
    }
}
