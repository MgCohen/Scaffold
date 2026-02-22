using System;
using VContainer;

namespace Scaffold.Containers
{
    internal sealed class VContainerResolver : IContainerResolver
    {
        private readonly IObjectResolver inner;

        internal VContainerResolver(IObjectResolver inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public T Resolve<T>()
        {
            return inner.Resolve<T>();
        }

        public void Inject(object obj)
        {
            inner.Inject(obj);
        }
    }
}
