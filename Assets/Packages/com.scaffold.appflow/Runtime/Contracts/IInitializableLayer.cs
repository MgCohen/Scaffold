using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.AppFlow
{
    public interface IInitializableLayer : IScopeLayer
    {
        Task InitializeAsync(ILayerInitRunner runner, CancellationToken ct);
    }
}
