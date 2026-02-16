using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Containers
{
    public abstract class Boostrap : LifetimeScope
    {
        [SerializeField] private ContainerConfig configFile;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ContainerConfig>(_ => configFile, Lifetime.Scoped);
            builder.Register<IContext>(_ => new Context(this, configFile), Lifetime.Scoped);
            builder.RegisterBuildCallback(o =>
            {
                IContext builder = o.Resolve<IContext>();
                Build(builder);
            });
        }

        protected abstract void Build(IContext context);
    }
}