using Scaffold.Containers.Adapters;
using UnityEngine;
using VContainer.Unity;
using VContainerBuilder = VContainer.IContainerBuilder;

namespace Scaffold.Containers
{
    public class Container
    {
        private Context context;
        private ContainerConfig config;
        private Transform transform;

        internal LifetimeScope Build(LifetimeScope scope, ContainerConfig config, Context context)
        {
            this.config = config;
            this.transform = scope.transform;
            this.context = context;
            return scope.CreateChild(builder => Build(builder));
        }

        private void Build(VContainerBuilder builder)
        {
            var adapter = new VContainerBuilderAdapter(builder);
            adapter.Register<IContext>(_ => context, ContainerLifetime.Scoped);
            Build(adapter, config, transform);
        }

        protected virtual void Build(IContainerBuilder builder, ContainerConfig config, Transform holder)
        {
        }
    }
}

