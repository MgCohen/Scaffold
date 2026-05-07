#nullable enable
using System.Diagnostics.CodeAnalysis;

namespace Scaffold.States
{
    public interface ICatalog
    {
        Ref<T> AllocateRef<T>();
        void RegisterAt<T>(Ref<T> @ref, T obj);

        Ref<T> Register<T>(T obj);

        T Resolve<T>(Ref<T> @ref);
        bool TryResolve<T>(Ref<T> @ref, [MaybeNullWhen(false)] out T obj);

        void Unregister<T>(Ref<T> @ref);

        void RegisterFactory<T>(ICatalogFactory<T> factory);
        void RegisterStub<T>(T stub);
    }
}
