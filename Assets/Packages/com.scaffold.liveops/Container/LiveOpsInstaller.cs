using Scaffold.CloudCode;
using Scaffold.LiveOps;
using VContainer;
using VContainer.Unity;

namespace Scaffold.LiveOps.Container
{
    /// <summary>
    /// Registers <see cref="ILiveOpsService"/> and related types. Install after <c>CloudCodeInstaller</c> so
    /// <see cref="CloudCodeOptimisticHandlerRegistry"/> and <see cref="CloudCodeErrorHandler"/> exist.
    /// Register GameApi optimistic handlers with <see cref="LiveOpsOptimisticRegistrationExtensions.RegisterOptimisticCloudCodeHandler{TImplementation}"/>
    /// or <c>builder.Register&lt;THandler&gt;(Lifetime.Singleton).As&lt;IOptimisticCloudCodeHandler&gt;().AsImplementedInterfaces()</c>.
    /// </summary>
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
