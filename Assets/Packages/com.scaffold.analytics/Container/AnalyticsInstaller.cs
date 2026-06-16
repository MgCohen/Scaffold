using VContainer;
using VContainer.Unity;

namespace Scaffold.Analytics
{
    public sealed class AnalyticsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<AnalyticsService>(Lifetime.Singleton)
                .AsImplementedInterfaces();
        }
    }
}
