#nullable enable
using System;

namespace Scaffold.States
{
    public interface ICatalogFactory<T>
    {
        Ref<T> CreateRef(T obj);
    }

    internal sealed class DefaultCatalogFactory<T> : ICatalogFactory<T>
    {
        public static readonly DefaultCatalogFactory<T> Instance = new DefaultCatalogFactory<T>();

        public Ref<T> CreateRef(T obj)
        {
            if (obj is null) throw new ArgumentNullException(nameof(obj));
            Guid id = obj is ICatalogged cat ? cat.Key : Guid.NewGuid();
            return new Ref<T>(id);
        }
    }
}
