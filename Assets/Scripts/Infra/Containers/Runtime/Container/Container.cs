using UnityEngine;
using VContainer;
using VContainer.Unity;

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
            return scope.CreateChild(Build);
        }

        private void Build(IContainerBuilder builder)
        {
            builder.Register<IContext>(_ => context, Lifetime.Scoped);
            Build(builder, config, transform);
        }

        protected virtual void Build(IContainerBuilder builder, ContainerConfig config, Transform holder)
        {

        }
    }
}
