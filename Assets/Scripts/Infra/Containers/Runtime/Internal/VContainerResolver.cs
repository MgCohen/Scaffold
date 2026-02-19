using System;
using VContainer;

namespace Scaffold.Containers
{
    internal sealed class VContainerResolver : IContainerResolver
    {
        private readonly IObjectResolver _inner;

        internal VContainerResolver(IObjectResolver inner)
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
