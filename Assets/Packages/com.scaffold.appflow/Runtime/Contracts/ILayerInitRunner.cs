using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VContainer;

namespace Scaffold.AppFlow
{
    public interface ILayerInitRunner
    {
        IObjectResolver Scope { get; }

        IReadOnlyList<IAsyncInitializable> PendingInitializables { get; }

        Task RunDefaultInitAsync(CancellationToken ct);
    }
}
