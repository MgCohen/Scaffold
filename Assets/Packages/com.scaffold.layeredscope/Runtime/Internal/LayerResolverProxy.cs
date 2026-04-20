using System;
using VContainer;

namespace Scaffold.LayeredScope.Internal
{
    internal sealed class LayerResolverProxy : ILayerResolver
    {
        public IObjectResolver Top =>
            top ?? throw new InvalidOperationException(
                "[LayeredScope] LayerResolverProxy not bound. Did you create an ApplicationHost?");

        private IObjectResolver top;

        internal void Bind(IObjectResolver newTop)
        {
            top = newTop;
        }

        public bool TryResolve<T>(out T value)
        {
            return Top.TryResolve(out value);
        }

        public T Resolve<T>()
        {
            return Top.Resolve<T>();
        }
    }
}
