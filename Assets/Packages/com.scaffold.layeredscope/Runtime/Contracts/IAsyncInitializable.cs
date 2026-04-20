using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.LayeredScope
{
    public interface IAsyncInitializable
    {
        Task InitializeAsync(CancellationToken ct);
    }
}
