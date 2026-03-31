using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

namespace Scaffold.MVVM.Binding
{
    internal class BindedCollection<TSource, TTarget> : IBindedCollection<TSource, TTarget>, IBind<ICollection<TSource>>
    {
        public BindedCollection(BindSet<TSource, TTarget> binding, ICollectionHandler<TSource, TTarget> handler, Action detach)
        {
            if (binding is null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            this.handler = handler;
            this.detach = detach;
        }

        private Dictionary<TSource, List<TTarget>> lookup = new Dictionary<TSource, List<TTarget>>();
        private readonly List<TTarget> trackedTargets = new List<TTarget>();
        private ICollectionHandler<TSource, TTarget> handler;
        private Action detach;
        private ICollection<TSource> source;
        private bool disposed;

        public void Update(ICollection<TSource> value)
        {
            if (ReferenceEquals(source, value))
            {
                return;
            }

            if (source is INotifyCollectionChanged oldObservable)
            {
                oldObservable.CollectionChanged -= HandleCollectionChanges;
            }

            ReplaceSourceCollection(value);
            source = value;
        }

        private void ReplaceSourceCollection(ICollection<TSource> value)
        {
            ClearTargets();
            if (value is INotifyCollectionChanged newObservable)
            {
                newObservable.CollectionChanged -= HandleCollectionChanges;
                newObservable.CollectionChanged += HandleCollectionChanges;
            }

            if (value == null)
            {
                return;
            }

            AddAllItems(value);
        }

        public void HandleCollectionChanges(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                if (sender is ICollection<TSource> resetCollection)
                {
                    ClearTargets();
                    AddAllItems(resetCollection);
                }

                return;
            }

            ApplyCollectionItems(e.OldItems, false);
            ApplyCollectionItems(e.NewItems, true);
        }

        private void ApplyCollectionItems(IList items, bool addItems)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                TSource sourceItem = (TSource)item;
                UpdateLookupItem(sourceItem, addItems);
            }
        }

        private void UpdateLookupItem(TSource sourceItem, bool addItems)
        {
            if (addItems)
            {
                List<TTarget> list = GetOrCreateTargets(sourceItem);
                TTarget target = handler.Add(sourceItem);
                list.Add(target);
                trackedTargets.Add(target);
                return;
            }

            TryPopTarget(sourceItem);
        }

        private List<TTarget> GetOrCreateTargets(TSource sourceItem)
        {
            if (lookup.TryGetValue(sourceItem, out List<TTarget> list))
            {
                return list;
            }

            list = new List<TTarget>();
            lookup[sourceItem] = list;
            return list;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            if (source is INotifyCollectionChanged observable)
            {
                observable.CollectionChanged -= HandleCollectionChanges;
            }

            ClearTargets();
            source = null;
            detach?.Invoke();
            detach = null;
        }

        public void Update()
        {
            if (source == null)
            {
                return;
            }

            Debug.Log("Collection Changed");
        }

        private void ClearTargets()
        {
            if (trackedTargets.Count == 0)
            {
                lookup.Clear();
                return;
            }

            for (int i = trackedTargets.Count - 1; i >= 0; i--)
            {
                handler.Remove(trackedTargets[i]);
            }

            trackedTargets.Clear();
            lookup.Clear();
        }

        private void AddAllItems(ICollection<TSource> value)
        {
            foreach (var item in value)
            {
                if (!lookup.TryGetValue(item, out List<TTarget> list))
                {
                    list = new List<TTarget>();
                    lookup[item] = list;
                }

                TTarget target = handler.Add(item);
                list.Add(target);
                trackedTargets.Add(target);
            }
        }

        private void TryPopTarget(TSource sourceItem)
        {
            if (!lookup.TryGetValue(sourceItem, out List<TTarget> existing) || existing.Count == 0)
            {
                return;
            }

            int lastIndex = existing.Count - 1;
            TTarget removed = existing[lastIndex];
            existing.RemoveAt(lastIndex);
            trackedTargets.Remove(removed);
            handler.Remove(removed);
        }
    }
}
