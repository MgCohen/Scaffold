using VContainer;

namespace Scaffold.LayeredScope
{
    public interface ILayerResolver
    {
        IObjectResolver Top { get; }
        bool TryResolve<T>(out T value);
        T Resolve<T>();
    }
}
