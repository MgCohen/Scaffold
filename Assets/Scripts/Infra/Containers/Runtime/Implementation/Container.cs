using Scaffold.Containers.Adapters;
using UnityEngine;
using VContainer.Unity;
using VContainerBuilder = VContainer.IContainerBuilder;

namespace Scaffold.Containers
{
    public abstract class Container
    {
        private Context context;
        private Transform transform;

        internal LifetimeScope Build(LifetimeScope scope, Context context)
        {
            this.transform = scope.transform;
            this.context = context;
            return scope.CreateChild(builder => Build(builder));
        }

        private void Build(VContainerBuilder builder)
        {
            var adapter = new VContainerBuilderAdapter(builder);
            adapter.Register<IContext>(_ => context, ContainerLifetime.Scoped);
            Build(adapter, transform);
        }

        protected virtual void Build(IContainerBuilder builder, Transform holder)
        {
        }
    }
}
