using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VContainer;

namespace Scaffold.AppFlow.Internal
{
    internal sealed class LayerInitRunner : ILayerInitRunner
    {
        public LayerInitRunner(IObjectResolver scope, IReadOnlyList<IAsyncInitializable> pending, IInLayerScheduler scheduler)
        {
            Scope = scope ?? throw new System.ArgumentNullException(nameof(scope));
            PendingInitializables = pending ?? System.Array.Empty<IAsyncInitializable>();
            this.scheduler = scheduler ?? throw new System.ArgumentNullException(nameof(scheduler));
        }

        public IObjectResolver Scope { get; }

        public IReadOnlyList<IAsyncInitializable> PendingInitializables { get; }

        public bool DefaultInitInvoked { get; private set; }

        private readonly IInLayerScheduler scheduler;

        public Task RunDefaultInitAsync(CancellationToken ct)
        {
            DefaultInitInvoked = true;
            return scheduler.RunAsync(PendingInitializables, ct);
        }
    }
}
