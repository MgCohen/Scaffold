using UnityEngine;
using VContainer.Unity;

namespace Scaffold.Containers
{
    internal sealed class VContainerScope : IContainerScope
    {
        private readonly LifetimeScope _scope;

        internal VContainerScope(LifetimeScope scope)
        {
            _scope = scope;
        }

        public Transform Transform => _scope.transform;

        public void BuildChild(Container container, Context childContext, Transform holder)
        {
            LifetimeScope childScope = _scope.CreateChild(b =>
            {
                var registry = new VContainerRegistry(b);
                registry.Register<IContext>(_ => childContext, ContainerLifetime.Scoped);
                registry.Register<IContainerResolver>(
                    _ => new VContainerResolver(_.Resolve<VContainer.IObjectResolver>()),
                    ContainerLifetime.Scoped);
                container.Build(registry, holder);
            });
            childContext.SetScope(new VContainerScope(childScope));
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
