using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Scaffold.Pooling
{
    /// <summary>
    /// Generic pool with separate idle (<see cref="Available"/>) and active (<see cref="Active"/>) tracking.
    /// </summary>
    public sealed class Pool<T>
    {
        public Pool(Func<T> factory, Action<T> onDestroy = null, int initialSize = 0, int maxSize = -1)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.onDestroy = onDestroy;
            if (initialSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialSize));
            }

            if (maxSize < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize));
            }

            this.maxAvailableSize = maxSize;

            for (int i = 0; i < initialSize; i++)
            {
                T created = this.factory();
                RejectNullFromFactory(created);
                this.available.Push(created);
            }
        }

        public int Available => available.Count;

        public IReadOnlyCollection<T> Active => new ReadOnlyCollection<T>(new List<T>(active));

        private readonly Func<T> factory;
        private readonly Action<T> onDestroy;
        private readonly int maxAvailableSize;
        private readonly Stack<T> available = new Stack<T>();
        private readonly HashSet<T> active = new HashSet<T>();
        private readonly Dictionary<T, Action> poolableReturnHandlers = new Dictionary<T, Action>();

        public T Take()
        {
            T item = TakeIdleOrCreate();
            if (!active.Add(item))
            {
                throw new InvalidOperationException("The pooled instance is already active.");
            }

            RegisterPoolableIfNeeded(item);
            return item;
        }

        private T TakeIdleOrCreate()
        {
            if (available.Count > 0)
            {
                return available.Pop();
            }

            T created = factory();
            RejectNullFromFactory(created);
            return created;
        }

        private void RegisterPoolableIfNeeded(T item)
        {
            if (item is not IPoolable poolable)
            {
                return;
            }

            Action handler = () => Return(item);
            poolable.ReturnRequested += handler;
            poolableReturnHandlers[item] = handler;
            poolable.OnTakenFromPool();
        }

        public void Return(T item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!active.Remove(item))
            {
                throw new InvalidOperationException("The item is not active in this pool.");
            }

            UnregisterPoolable(item);
            PushIdleOrDestroy(item);
        }

        private void PushIdleOrDestroy(T item)
        {
            if (maxAvailableSize >= 0 && available.Count >= maxAvailableSize)
            {
                Destroy(item);
                return;
            }

            available.Push(item);
        }

        public void Clear()
        {
            ClearActiveInstances();
            ClearIdleInstances();
        }

        private void ClearActiveInstances()
        {
            foreach (T item in new List<T>(active))
            {
                UnregisterPoolable(item);
                Destroy(item);
            }

            active.Clear();
            poolableReturnHandlers.Clear();
        }

        private void ClearIdleInstances()
        {
            while (available.Count > 0)
            {
                Destroy(available.Pop());
            }
        }

        private void UnregisterPoolable(T item)
        {
            if (item is not IPoolable poolable)
            {
                return;
            }

            if (poolableReturnHandlers.TryGetValue(item, out Action handler))
            {
                poolable.ReturnRequested -= handler;
                poolableReturnHandlers.Remove(item);
            }

            poolable.OnReturnedToPool();
        }

        private void Destroy(T item)
        {
            onDestroy?.Invoke(item);
        }

        private void RejectNullFromFactory(T item)
        {
            if (item is null)
            {
                throw new InvalidOperationException("Factory returned null.");
            }
        }
    }
}
