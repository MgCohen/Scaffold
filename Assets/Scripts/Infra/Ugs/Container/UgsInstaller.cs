using Scaffold.Scope.Contracts;
using Scaffold.Ugs;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Ugs.Container
{
    public sealed class UgsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<Ugs>(Lifetime.Singleton).AsSelf().As<IAsyncLayerInitializable>();
        }
    }
}
