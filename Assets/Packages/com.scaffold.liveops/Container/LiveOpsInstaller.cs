using Scaffold.LiveOps;
using Scaffold.Scope.Contracts;
using VContainer;
using VContainer.Unity;

namespace Scaffold.LiveOps.Container
{
    public sealed class LiveOpsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<LiveOpsService>(Lifetime.Singleton)
                .As<ILiveOpsService>()
                .As<IAsyncLayerInitializable>();
        }
    }
}
