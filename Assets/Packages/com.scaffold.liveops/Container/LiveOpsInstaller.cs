using Scaffold.CloudCode;
using Scaffold.LiveOps;
using VContainer;
using VContainer.Unity;

namespace Scaffold.LiveOps.Container
{
    // sample: registers ILiveOpsService; install after CloudCodeInstaller. Register handlers via LiveOpsOptimisticRegistrationExtensions or As<IOptimisticCloudCodeHandler>().
    public sealed class LiveOpsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<LiveOpsService>(Lifetime.Singleton)
                .As<ILiveOpsService>()
                .As<Scaffold.LayeredScope.IAsyncInitializable>();
        }
    }
}
