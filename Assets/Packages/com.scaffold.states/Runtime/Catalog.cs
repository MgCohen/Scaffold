#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Scaffold.Maps;

namespace Scaffold.States
{
    internal sealed class Catalog : ICatalog
    {
        private static readonly object AllocatedSentinel = new object();

        private readonly Map<Reference, Type, object> entries = new Map<Reference, Type, object>();

        public Ref<T> AllocateRef<T>()
        {
            var @ref = new Ref<T>(Guid.NewGuid());
            entries.Add(@ref, typeof(T), AllocatedSentinel);
            return @ref;
        }

        public void RegisterAt<T>(Ref<T> @ref, T obj)
        {
            if (@ref is null) throw new ArgumentNullException(nameof(@ref));
            if (obj is null) throw new ArgumentNullException(nameof(obj));

            if (!entries.TryGetValue(@ref, typeof(T), out object current))
            {
                throw new InvalidOperationException(
                    $"Ref {@ref} was not allocated under {typeof(T).Name}; cannot RegisterAt.");
            }

            if (ReferenceEquals(current, AllocatedSentinel))
            {
                entries[@ref, typeof(T)] = obj!;
                return;
            }

            if (!ReferenceEquals(current, obj))
            {
                throw new InvalidOperationException(
                    $"Ref {@ref} is already bound to a different object.");
            }
        }

        public Ref<T> Register<T>(T obj)
        {
            if (obj is null) throw new ArgumentNullException(nameof(obj));

            Guid id = obj is ICatalogged cat ? cat.Key : Guid.NewGuid();
            var @ref = new Ref<T>(id);

            if (entries.TryGetValue(@ref, typeof(T), out object current))
            {
                if (ReferenceEquals(current, AllocatedSentinel))
                {
                    entries[@ref, typeof(T)] = obj!;
                    return @ref;
                }

                if (ReferenceEquals(current, obj))
                {
                    return @ref;
                }

                throw new InvalidOperationException(
                    $"Ref {@ref} is already bound to a different object (likely an ICatalogged.Key collision).");
            }

            entries.Add(@ref, typeof(T), obj!);
            return @ref;
        }

        public T Resolve<T>(Ref<T> @ref)
        {
            if (@ref is null) throw new ArgumentNullException(nameof(@ref));

            if (!entries.TryGetValue(@ref, typeof(T), out object current)
                || ReferenceEquals(current, AllocatedSentinel))
            {
                throw new KeyNotFoundException(
                    $"No binding for {@ref} under {typeof(T).Name}.");
            }

            return (T)current;
        }

        public bool TryResolve<T>(Ref<T> @ref, [MaybeNullWhen(false)] out T obj)
        {
            if (@ref is null)
            {
                obj = default;
                return false;
            }

            if (!entries.TryGetValue(@ref, typeof(T), out object current)
                || ReferenceEquals(current, AllocatedSentinel))
            {
                obj = default;
                return false;
            }

            obj = (T)current;
            return true;
        }

        public void Unregister<T>(Ref<T> @ref)
        {
            if (@ref is null) return;
            entries.Remove(@ref, typeof(T));
        }
    }
}
