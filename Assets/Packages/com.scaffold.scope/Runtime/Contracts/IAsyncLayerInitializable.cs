using System.Threading;
using System.Threading.Tasks;
using VContainer;

namespace Scaffold.Scope.Contracts
{
    public interface IAsyncLayerInitializable
    {
        Task InitializeAsync(IObjectResolver resolver, CancellationToken cancellationToken);
    }
}
