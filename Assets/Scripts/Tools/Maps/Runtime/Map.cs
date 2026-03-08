using System;
using System.Collections.Generic;

namespace Scaffold.Maps
{
    public class Map<TPrimary, TSecondary, TValue> : BaseMap<Index<TPrimary, TSecondary>, TValue>
    {
        public Map()
        {
            predicateIndexers = new Dictionary<string, Indexer<TPrimary, TSecondary, TValue>>();
        }

        private readonly Dictionary<string, Indexer<TPrimary, TSecondary, TValue>> predicateIndexers;

        public TValue this[TPrimary primary, TSecondary secondary]
        {
            get
            {
                Index<TPrimary, TSecondary> index = CreateIndex(primary, secondary);
                return this[index];
            }
            set
            {
                Index<TPrimary, TSecondary> index = CreateIndex(primary, secondary);
                bool hasHolder = TryGetHolder(index, out Holder<TValue> holder);
                if (hasHolder)
                {
                    holder.Value = value;
                }
                else
                {
                    Add(index, value);
                }
            }
        }

        public override TValue this[Index<TPrimary, TSecondary> index]
        {
            get
            {
                return base[index];
            }
            set
            {
                bool hasHolder = TryGetHolder(index, out Holder<TValue> holder);
                if (hasHolder)
                {
                    holder.Value = value;
                }
                else
                {
                    Add(index, value);
                }
            }
        }

        public void Add(TPrimary primary, TValue value)
        {
            Add(primary, default, value);
        }

        public void Add(TSecondary secondary, TValue value)
        {
            Add(default, secondary, value);
        }

        public void Add(TPrimary primary, TSecondary secondary, TValue value)
        {
            Index<TPrimary, TSecondary> index = CreateIndex(primary, secondary);
            Add(index, value);
        }

        public void Add(Index<TPrimary, TSecondary> index, TValue value)
        {
            Holder<TValue> holder = new Holder<TValue>(value);
            base.Add(index, holder);
            TrackEntry(index, holder);
        }

        public bool Contains(TPrimary primary, TSecondary secondary)
        {
            Index<TPrimary, TSecondary> index = CreateIndex(primary, secondary);
            return ContainsKey(index);
        }

        public bool TryGetValue(TPrimary primary, TSecondary secondary, out TValue value)
        {
            Index<TPrimary, TSecondary> index = CreateIndex(primary, secondary);
            return TryGetValue(index, out value);
        }

        public Indexer<TPrimary, TSecondary, TValue> AddIndexer(string name, Func<TPrimary, TSecondary, bool> predicate)
        {
            Indexer<TPrimary, TSecondary, TValue> indexer = CreateIndexer(name, predicate);
            RegisterIndexer(indexer);
            return indexer;
        }

        public bool TryGetIndexer(string name, out Indexer<TPrimary, TSecondary, TValue> indexer)
        {
            return predicateIndexers.TryGetValue(name, out indexer);
        }

        public bool RemoveIndexer(string name)
        {
            return predicateIndexers.Remove(name);
        }

        public IReadOnlyCollection<TValue> GetIndexedValues(string name)
        {
            Indexer<TPrimary, TSecondary, TValue> indexer = predicateIndexers[name];
            return indexer.Values;
        }

        public bool Remove(TPrimary primary, TSecondary secondary)
        {
            Index<TPrimary, TSecondary> index = CreateIndex(primary, secondary);
            return Remove(index);
        }

        public override bool Remove(Index<TPrimary, TSecondary> index)
        {
            bool hasHolder = TryGetHolder(index, out Holder<TValue> holder);
            if (hasHolder == false)
            {
                return false;
            }
            return RemoveTrackedEntry(index, holder);
        }

        public override void Clear()
        {
            base.Clear();
            ClearIndexers();
        }

        private bool RemoveTrackedEntry(Index<TPrimary, TSecondary> index, Holder<TValue> holder)
        {
            bool wasRemoved = base.Remove(index);
            if (wasRemoved)
            {
                UntrackEntry(holder);
            }
            return wasRemoved;
        }

        private Indexer<TPrimary, TSecondary, TValue> CreateIndexer(string name, Func<TPrimary, TSecondary, bool> predicate)
        {
            Indexer<TPrimary, TSecondary, TValue> indexer = new Indexer<TPrimary, TSecondary, TValue>(name, predicate);
            var entries = GetEntries();
            indexer.Rebuild(entries);
            return indexer;
        }

        private void RegisterIndexer(Indexer<TPrimary, TSecondary, TValue> indexer)
        {
            predicateIndexers.Add(indexer.Name, indexer);
        }

        private void TrackEntry(Index<TPrimary, TSecondary> index, Holder<TValue> holder)
        {
            foreach (Indexer<TPrimary, TSecondary, TValue> indexer in predicateIndexers.Values)
            {
                indexer.Track(index, holder);
            }
        }

        private void UntrackEntry(Holder<TValue> holder)
        {
            foreach (Indexer<TPrimary, TSecondary, TValue> indexer in predicateIndexers.Values)
            {
                indexer.Untrack(holder);
            }
        }

        private void ClearIndexers()
        {
            foreach (Indexer<TPrimary, TSecondary, TValue> indexer in predicateIndexers.Values)
            {
                indexer.Clear();
            }
        }

        private Index<TPrimary, TSecondary> CreateIndex(TPrimary primary, TSecondary secondary)
        {
            return new Index<TPrimary, TSecondary>(primary, secondary);
        }
    }
}
