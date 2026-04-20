using Scaffold.CloudCode;
using VContainer;

namespace Scaffold.LiveOps.Container
{
    /// <summary>
    /// Helpers to register optimistic Cloud Code handlers discoverable by <see cref="CloudCodeOptimisticHandlerRegistry"/>.
    /// Handlers must be <strong>singleton</strong>; the registry caches resolved instances per (request, response) type pair.
    /// </summary>
    public static class LiveOpsOptimisticRegistrationExtensions
    {
        public static void RegisterOptimisticCloudCodeHandler<TImplementation>(this IContainerBuilder builder, Lifetime lifetime)
            where TImplementation : class, IOptimisticCloudCodeHandler
        {
            builder.Register<TImplementation>(lifetime)
                .As<IOptimisticCloudCodeHandler>()
                .AsImplementedInterfaces();
        }
    }
}
