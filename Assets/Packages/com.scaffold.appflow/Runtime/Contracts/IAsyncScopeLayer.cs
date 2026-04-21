using System.Threading;
using System.Threading.Tasks;
using VContainer;

namespace Scaffold.AppFlow
{
    public interface IAsyncScopeLayer : IScopeLayer
    {
        Task PrepareAsync(IObjectResolver parent, CancellationToken ct);
    }
}
