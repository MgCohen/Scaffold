using UnityEngine;
using VContainer.Unity;

namespace Scaffold.Containers
{
    internal sealed class VContainerScope : IContainerScope
    {
        private readonly LifetimeScope scope;

        internal VContainerScope(LifetimeScope scope)
        {
            this.scope = scope;
        }

        public Transform Transform
        {
            get { return scope.transform; }
        }

        public void BuildChild(Container container, Context childContext, Transform holder)
        {
            LifetimeScope childScope = scope.CreateChild(b => BuildChildScope(b, container, childContext, holder));
            var childVContainerScope = new VContainerScope(childScope);
            childContext.SetScope(childVContainerScope);
        }

        private void BuildChildScope(VContainer.IContainerBuilder b, Container container, Context childContext, Transform holder)
        {
            var registry = new VContainerRegistry(b);
            registry.Register<IContext>(_ => childContext, ContainerLifetime.Scoped);
            registry.Register<IContainerResolver>(resolver => CreateContainerResolver(resolver), ContainerLifetime.Scoped);
            container.Build(registry, holder);
        }

        private IContainerResolver CreateContainerResolver(IContainerResolver resolver)
        {
            var objectResolver = resolver.Resolve<VContainer.IObjectResolver>();
            return new VContainerResolver(objectResolver);
        }

        public void Dispose()
        {
            scope.Dispose();
        }
    }
}
