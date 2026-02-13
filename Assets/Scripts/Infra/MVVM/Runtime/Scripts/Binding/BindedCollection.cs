using Scaffold.MVVM.Binding;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

namespace Scaffold.MVVM.Binding
{
    public class BindedCollection<TSource, TTarget> : IBindedCollection<TSource, TTarget>, IBind<ICollection<TSource>>
    {
        public BindedCollection(BindSet<TSource, TTarget> binding, ICollectionHandler<TSource, TTarget> handler)
        {
            this.handler = handler;
        }

        private Dictionary<TSource, List<TTarget>> lookup = new Dictionary<TSource, List<TTarget>>();
        private ICollectionHandler<TSource, TTarget> handler;
        private ICollection<TSource> source;

        public void Update(ICollection<TSource> value)
        {
            if(source == value)
            {
                return;
            }

            if(source != null)
            {
                Dispose();
            }
            if(value != null)
            {
                FillInitialCollection(value);
            }
            source = value;
        }

        private void FillInitialCollection(IEnumerable<TSource> sourceCollection)
        {
            if (sourceCollection is INotifyCollectionChanged ncc)
            {
                ncc.CollectionChanged -= HandleCollectionChanges;
                ncc.CollectionChanged += HandleCollectionChanges;
            }

            foreach (var source in sourceCollection)
            {
                AddItem(source);
            }
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

        private void HandleCollectionChanges(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    AddItem((TSource)item);
                }
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    RemoveItem((TSource)item);
                }
            }
        }


        public void Update()
        {
            Debug.Log("Collection Changed");
            //var source = getter();
            //source.Clear();
            //IEnumerable<TSource> sourceCollection = getter();
            //if (sourceCollection is INotifyCollectionChanged ncc)
            //{
            //    ncc.CollectionChanged -= HandleCollectionChanges;
            //    ncc.CollectionChanged += HandleCollectionChanges;
            //}

            ////remove all elements that are not present in the new list
            //foreach (var kvp in lookup)
            //{
            //    var count = kvp.Value.Count;
            //    for (int i = 0; i < count; i++)
            //    {
            //        RemoveItem(kvp.Key);
            //    }
            //}

            //foreach (var source in sourceCollection)
            //{
            //    AddItem(source);
            //}
        }

        public void Dispose()
        {
            if (source is INotifyCollectionChanged ncc)
            {
                ncc.CollectionChanged -= HandleCollectionChanges;
            }
        }

    }
}
