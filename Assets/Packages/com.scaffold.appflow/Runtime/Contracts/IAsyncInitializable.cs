using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.AppFlow
{
    public interface IAsyncInitializable
    {
        Task InitializeAsync(CancellationToken ct);
    }
}
