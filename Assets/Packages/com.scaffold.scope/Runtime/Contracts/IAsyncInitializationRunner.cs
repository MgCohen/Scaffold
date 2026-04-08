using System.Threading;
using System.Threading.Tasks;
using VContainer;

namespace Scaffold.Scope.Contracts
{
    public interface IAsyncInitializationRunner
    {
        Task RunAsync(IObjectResolver resolver, CancellationToken cancellationToken);
    }
}
