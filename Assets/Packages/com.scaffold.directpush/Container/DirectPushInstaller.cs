using Scaffold.LayeredScope;
using VContainer;
using VContainer.Unity;

namespace Scaffold.DirectPush
{
    public sealed class DirectPushInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<PushSubscriptionService>(Lifetime.Singleton)
                .AsSelf()
                .As<IAsyncInitializable>();

            builder.Register<DirectPushClient>(Lifetime.Singleton);

            builder.Register<PushDisconnectHandler>(Lifetime.Singleton)
                .AsSelf()
                .As<IAsyncInitializable>();
        }
    }
}
