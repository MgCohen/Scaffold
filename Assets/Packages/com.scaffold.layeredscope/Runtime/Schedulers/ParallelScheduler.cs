using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.LayeredScope
{
    internal sealed class ParallelScheduler : IInLayerScheduler
    {
        public Task RunAsync(IReadOnlyList<IAsyncInitializable> fresh, CancellationToken ct)
        {
            if (fresh == null || fresh.Count == 0)
            {
                return Task.CompletedTask;
            }

            return Task.WhenAll(fresh.Select(p => p.InitializeAsync(ct)));
        }
    }
}
