using System;
using UnityEngine;
using VContainer.Unity;

namespace Scaffold.Containers
{
    internal sealed class VContainerAdapter : IContainerAdapter
    {
        public void Run(Transform root, Action<IContext> build)
        {
            var go = new GameObject("[VContainerRoot]");
            go.transform.SetParent(root);
            go.SetActive(false);
            var scope = go.AddComponent<VContainerRootScope>();
            scope.SetBuild(build);
            go.SetActive(true);
        }

        private sealed class VContainerRootScope : LifetimeScope
        {
            private Action<IContext> _build;

            public void SetBuild(Action<IContext> build) => _build = build;

            protected override void Configure(VContainer.IContainerBuilder builder)
            {
                var rootScope = new VContainerScope(this);
                var rootContext = new Context(rootScope);
                var registry = new VContainerRegistry(builder);
                registry.Register<IContext>(_ => rootContext, ContainerLifetime.Scoped);
                registry.RegisterBuildCallback(_ => _build(rootContext));
            }
        }
    }
}
