using Scaffold.Containers.Adapters;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Containers
{
    public abstract class Boostrap : LifetimeScope
    {
        [SerializeField] private ContainerConfig configFile;

        protected override void Configure(VContainer.IContainerBuilder builder)
        {
            var adapter = new VContainerBuilderAdapter(builder);

            adapter.Register<ContainerConfig>(_ => configFile, ContainerLifetime.Scoped);
            adapter.Register<IContext>(_ => new Context(this, configFile), ContainerLifetime.Scoped);
            adapter.RegisterBuildCallback(o =>
            {
                IContext context = o.Resolve<IContext>();
                Build(context);
            });
        }

        protected abstract void Build(IContext context);
    }
}