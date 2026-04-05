using Scaffold.Scope.Contracts;
using VContainer;
using VContainer.Unity;

namespace Scaffold.DirectPush
{
    /// <summary>
    /// VContainer installer for the DirectPush module.
    /// Registers <see cref="PushSubscriptionService"/> (receiving) and <see cref="DirectPushClient"/> (sending).
    /// </summary>
    public sealed class DirectPushInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<PushSubscriptionService>(Lifetime.Singleton)
                .AsSelf()
                .As<IAsyncLayerInitializable>();

            builder.Register<DirectPushClient>(Lifetime.Singleton);

            builder.Register<PushDisconnectHandler>(Lifetime.Singleton)
                .AsSelf()
                .As<IAsyncLayerInitializable>();
        }
    }
}
