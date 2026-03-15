using Scaffold.MVVM.Binding;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

namespace Scaffold.MVVM.Binding
{
    internal class BindedCollection<TSource, TTarget> : IBindedCollection<TSource, TTarget>, IBind<ICollection<TSource>>
    {
        public BindedCollection(BindSet<TSource, TTarget> binding, ICollectionHandler<TSource, TTarget> handler, Action detach)
        {
            if (binding is null) { throw new ArgumentNullException(nameof(binding)); }
            if (handler is null) { throw new ArgumentNullException(nameof(handler)); }
            this.handler = handler;
            this.detach = detach;
        }

        private Dictionary<TSource, List<TTarget>> lookup = new Dictionary<TSource, List<TTarget>>();
        private ICollectionHandler<TSource, TTarget> handler;
        private Action detach;
        private ICollection<TSource> source;
        private bool disposed;

        public void Update(ICollection<TSource> value)
        {
            if (source == value) { return; }
            ReplaceSource(value);
            source = value;
        }

        private void ReplaceSource(ICollection<TSource> value)
        {
            if (source != null) { Dispose(); }
            if (value != null) { FillInitialCollection(value); }
        }

        private void FillInitialCollection(IEnumerable<TSource> sourceCollection)
        {
            SubscribeIfObservable(sourceCollection);
            foreach (var s in sourceCollection) { AddItem(s); }
        }

        private void SubscribeIfObservable(IEnumerable<TSource> sourceCollection)
        {
            if (sourceCollection is not INotifyCollectionChanged ncc) { return; }
            ncc.CollectionChanged -= HandleCollectionChanges;
            ncc.CollectionChanged += HandleCollectionChanges;
        }

        private void HandleCollectionChanges(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) { ProcessNewItems(e); }
            if (e.OldItems != null) { ProcessOldItems(e); }
        }

        private void ProcessNewItems(NotifyCollectionChangedEventArgs e)
        {
            foreach (var item in e.NewItems) { AddItem((TSource)item); }
        }

        private void ProcessOldItems(NotifyCollectionChangedEventArgs e)
        {
            foreach (var item in e.OldItems) { RemoveItem((TSource)item); }
        }

        private void AddItem(TSource source)
        {
            if (!lookup.TryGetValue(source, out List<TTarget> list))
            {
                list = new List<TTarget>();
                lookup[source] = list;
            }
            TTarget item = handler.Add(source);
            list.Add(item);
        }

        private void RemoveItem(TSource source)
        {
            if (!lookup.TryGetValue(source, out List<TTarget> list))
            {
                return;
            }
            TTarget item = list[^1];
            list.Remove(item);
            handler.Remove(item);
        }

        public void Update()
        {
            if (source == null) { return; }
            Debug.Log("Collection Changed");
        }

        public void Dispose()
        {
            if (disposed) { return; }
            disposed = true;
            if (source is INotifyCollectionChanged ncc)
            {
                ncc.CollectionChanged -= HandleCollectionChanges;
            }
            source = null;
            detach?.Invoke();
            detach = null;
        }
    }
}


