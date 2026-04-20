using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.LayeredScope
{
    public interface IInLayerScheduler
    {
        Task RunAsync(IReadOnlyList<IAsyncInitializable> fresh, CancellationToken ct);
    }
}
