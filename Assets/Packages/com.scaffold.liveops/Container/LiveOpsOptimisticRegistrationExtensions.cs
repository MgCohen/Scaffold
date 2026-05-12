using Scaffold.CloudCode;
using VContainer;

namespace Scaffold.LiveOps.Container
{
    // sample: register singleton IOptimisticCloudCodeHandler instances for CloudCodeOptimisticHandlerRegistry discovery.
    public static class LiveOpsOptimisticRegistrationExtensions
    {
        public static void RegisterOptimisticCloudCodeHandler<TImplementation>(this IContainerBuilder builder, Lifetime lifetime) where TImplementation : class, IOptimisticCloudCodeHandler
        {
            builder.Register<TImplementation>(lifetime).AsImplementedInterfaces();
        }
    }
}
