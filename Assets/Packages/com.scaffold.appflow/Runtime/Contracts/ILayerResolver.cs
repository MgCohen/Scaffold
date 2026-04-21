using VContainer;

namespace Scaffold.AppFlow
{
    public interface ILayerResolver
    {
        IObjectResolver Top { get; }
        bool TryResolve<T>(out T value);
        T Resolve<T>();
    }
}
