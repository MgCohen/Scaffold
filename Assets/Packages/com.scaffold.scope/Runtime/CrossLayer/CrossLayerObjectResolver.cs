using System;
using System.Collections.Generic;
using Scaffold.Scope.Contracts;
using VContainer;

namespace Scaffold.Scope
{
    public sealed class CrossLayerObjectResolver : ICrossLayerObjectResolver
    {
        private readonly List<IObjectResolver> resolvers = new List<IObjectResolver>();
        private readonly object gate = new object();

        public void Reset()
        {
            lock (gate)
            {
                resolvers.Clear();
            }
        }

        public void RegisterScope(IObjectResolver resolver)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            lock (gate)
            {
                if (resolvers.Contains(resolver))
                {
                    return;
                }

                resolvers.Add(resolver);
            }
        }

        public void Inject(object instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            IObjectResolver[] snapshot = SnapshotResolvers();
            Exception lastException = TryInjectFromResolvers(instance, snapshot);
            if (lastException != null)
            {
                throw new InvalidOperationException("Failed to inject the instance from any registered scope resolver.", lastException);
            }
        }

        public T Resolve<T>()
        {
            if (TryResolve(out T instance))
            {
                return instance;
            }

            throw new InvalidOperationException($"Type '{typeof(T).FullName}' could not be resolved from any registered scope.");
        }

        public object Resolve(Type type)
        {
            if (TryResolve(type, out object instance))
            {
                return instance;
            }

            throw new InvalidOperationException($"Type '{type?.FullName}' could not be resolved from any registered scope.");
        }

        public bool TryResolve<T>(out T instance)
        {
            if (TryResolve(typeof(T), out object resolved))
            {
                instance = (T)resolved;
                return true;
            }

            instance = default;
            return false;
        }

        public bool TryResolve(Type type, out object instance)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            IObjectResolver[] snapshot = SnapshotResolvers();
            for (int i = snapshot.Length - 1; i >= 0; i--)
            {
                if (TryResolveOne(type, snapshot[i], out instance))
                {
                    return true;
                }
            }

            instance = null;
            return false;
        }

        private IObjectResolver[] SnapshotResolvers()
        {
            lock (gate)
            {
                return resolvers.ToArray();
            }
        }

        private Exception TryInjectFromResolvers(object instance, IObjectResolver[] snapshot)
        {
            Exception lastException = null;
            for (int i = snapshot.Length - 1; i >= 0; i--)
            {
                if (TryInjectOne(instance, snapshot[i], ref lastException))
                {
                    return null;
                }
            }

            return lastException;
        }

        private bool TryInjectOne(object instance, IObjectResolver resolver, ref Exception lastException)
        {
            if (resolver == null)
            {
                return false;
            }

            try
            {
                resolver.Inject(instance);
                return true;
            }
            catch (Exception exception)
            {
                lastException = exception;
                return false;
            }
        }

        private bool TryResolveOne(Type type, IObjectResolver resolver, out object instance)
        {
            instance = null;
            if (resolver == null)
            {
                return false;
            }

            try
            {
                instance = resolver.Resolve(type);
                return true;
            }
            catch (VContainerException)
            {
                return false;
            }
        }
    }
}
