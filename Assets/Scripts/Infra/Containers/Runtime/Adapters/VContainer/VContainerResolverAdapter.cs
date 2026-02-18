using System;
using VContainer;

namespace Scaffold.Containers.Adapters
{
    /// <summary>
    /// Adapter that wraps VContainer.IObjectResolver and exposes the project-level IContainerResolver interface.
    /// </summary>
    internal sealed class VContainerResolverAdapter : IContainerResolver
    {
        private readonly IObjectResolver _inner;

        public VContainerResolverAdapter(IObjectResolver inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public T Resolve<T>()
        {
            return _inner.Resolve<T>();
        }

        public void Inject(object obj)
        {
            _inner.Inject(obj);
        }
    }
}
