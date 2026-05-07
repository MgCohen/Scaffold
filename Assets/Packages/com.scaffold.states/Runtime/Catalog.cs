#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Scaffold.States
{
    internal sealed class Catalog : ICatalog
    {
        private readonly Dictionary<Type, ITypeCatalog> buckets = new Dictionary<Type, ITypeCatalog>();

        public Ref<T> AllocateRef<T>() => Bucket<T>().AllocateRef();

        public void RegisterAt<T>(Ref<T> @ref, T obj) => Bucket<T>().RegisterAt(@ref, obj);

        public Ref<T> Register<T>(T obj) => Bucket<T>().Register(obj);

        public T Resolve<T>(Ref<T> @ref) => Bucket<T>().Resolve(@ref);

        public bool TryResolve<T>(Ref<T> @ref, [MaybeNullWhen(false)] out T obj) => Bucket<T>().TryResolve(@ref, out obj);

        public void Unregister<T>(Ref<T> @ref) => Bucket<T>().Unregister(@ref);

        public void RegisterFactory<T>(ICatalogFactory<T> factory)
        {
            if (factory is null) throw new ArgumentNullException(nameof(factory));
            Bucket<T>().Factory = factory;
        }

        public void RegisterStub<T>(T stub)
        {
            if (stub is null) throw new ArgumentNullException(nameof(stub));
            Bucket<T>().SetStub(stub);
        }

        private TypeCatalog<T> Bucket<T>()
        {
            if (!buckets.TryGetValue(typeof(T), out ITypeCatalog? existing))
            {
                var created = new TypeCatalog<T>();
                buckets[typeof(T)] = created;
                return created;
            }

            return (TypeCatalog<T>)existing;
        }

        private interface ITypeCatalog { }

        private sealed class TypeCatalog<T> : ITypeCatalog
        {
            private readonly Dictionary<Ref<T>, T> entries = new Dictionary<Ref<T>, T>();
            private readonly HashSet<Ref<T>> allocated = new HashSet<Ref<T>>();

            private T stub = default!;
            private bool hasStub;

            public ICatalogFactory<T> Factory { get; set; } = DefaultCatalogFactory<T>.Instance;

            public void SetStub(T value)
            {
                stub = value;
                hasStub = true;
            }

            public Ref<T> AllocateRef()
            {
                var @ref = new Ref<T>(Guid.NewGuid());
                allocated.Add(@ref);
                return @ref;
            }

            public Ref<T> Register(T obj)
            {
                if (obj is null) throw new ArgumentNullException(nameof(obj));

                Ref<T> @ref = Factory.CreateRef(obj);
                if (@ref is null)
                {
                    throw new InvalidOperationException(
                        $"Catalog factory for {typeof(T).Name} returned a null Ref.");
                }

                if (entries.TryGetValue(@ref, out T? current))
                {
                    if (ReferenceEquals(current, obj))
                    {
                        return @ref;
                    }

                    throw new InvalidOperationException(
                        $"Ref {@ref} is already bound to a different object (likely an ICatalogged.Key collision).");
                }

                entries[@ref] = obj;
                allocated.Remove(@ref);
                return @ref;
            }

            public void RegisterAt(Ref<T> @ref, T obj)
            {
                if (@ref is null) throw new ArgumentNullException(nameof(@ref));
                if (obj is null) throw new ArgumentNullException(nameof(obj));

                if (entries.TryGetValue(@ref, out T? current))
                {
                    if (ReferenceEquals(current, obj))
                    {
                        return;
                    }

                    throw new InvalidOperationException(
                        $"Ref {@ref} is already bound to a different object.");
                }

                if (!allocated.Contains(@ref))
                {
                    throw new InvalidOperationException(
                        $"Ref {@ref} was not allocated under {typeof(T).Name}; cannot RegisterAt.");
                }

                entries[@ref] = obj;
                allocated.Remove(@ref);
            }

            public T Resolve(Ref<T> @ref)
            {
                if (@ref is null) throw new ArgumentNullException(nameof(@ref));

                if (entries.TryGetValue(@ref, out T? value))
                {
                    return value;
                }

                if (hasStub)
                {
                    return stub;
                }

                throw new KeyNotFoundException(
                    $"No binding for {@ref} under {typeof(T).Name}.");
            }

            public bool TryResolve(Ref<T> @ref, [MaybeNullWhen(false)] out T obj)
            {
                if (@ref is null)
                {
                    obj = default;
                    return false;
                }

                if (entries.TryGetValue(@ref, out T? value))
                {
                    obj = value;
                    return true;
                }

                obj = default;
                return false;
            }

            public void Unregister(Ref<T> @ref)
            {
                if (@ref is null) return;
                entries.Remove(@ref);
                allocated.Remove(@ref);
            }
        }
    }
}
