using System;
using System.Collections.Generic;

namespace Scaffold.Maps
{
    public class Map<TPrimary, TSecondary, TValue> : BaseMap<Index<TPrimary, TSecondary>, TValue>, IReadOnlyMap<TPrimary, TSecondary, TValue>
    {
        public Map()
        {
            predicateIndexers = new Dictionary<string, Indexer<TPrimary, TSecondary, TValue>>();
        }

        public TValue this[TPrimary primary, TSecondary secondary]
        {
            get
            {
                Index<TPrimary, TSecondary> index = new Index<TPrimary, TSecondary>(primary, secondary);
                return this[index];
            }
            set
            {
                Index<TPrimary, TSecondary> index = new Index<TPrimary, TSecondary>(primary, secondary);
                if (TryGetHolder(index, out Holder<TValue> holder))
                {
                    holder.Value = value;
                    return;
                }

                Add(index, value);
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
                if (TryGetHolder(index, out Holder<TValue> holder))
                {
                    holder.Value = value;
                    return;
                }

                Add(index, value);
            }
        }

        private readonly Dictionary<string, Indexer<TPrimary, TSecondary, TValue>> predicateIndexers;

        public void Add(TPrimary primary, TValue value)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            Add(primary, default, value);
        }

        public void Add(TSecondary secondary, TValue value)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            Add(default, secondary, value);
        }

        public void Add(TPrimary primary, TSecondary secondary, TValue value)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            Index<TPrimary, TSecondary> index = new Index<TPrimary, TSecondary>(primary, secondary);
            Add(index, value);
        }

        public void Add(Index<TPrimary, TSecondary> index, TValue value)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            Holder<TValue> holder = new Holder<TValue>(value);
            base.Add(index, holder);
            foreach (Indexer<TPrimary, TSecondary, TValue> indexer in predicateIndexers.Values)
            {
                indexer.Track(index, holder);
            }
        }

        public bool Contains(TPrimary primary, TSecondary secondary)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            Index<TPrimary, TSecondary> index = new Index<TPrimary, TSecondary>(primary, secondary);
            return ContainsKey(index);
        }

        public bool TryGetValue(TPrimary primary, TSecondary secondary, out TValue value)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            Index<TPrimary, TSecondary> index = new Index<TPrimary, TSecondary>(primary, secondary);
            return TryGetValue(index, out value);
        }

        public Indexer<TPrimary, TSecondary, TValue> AddIndexer(string name, Func<TPrimary, TSecondary, bool> predicate)
        {
            if (predicateIndexers == null) throw new InvalidOperationException("Map indexers were not initialized.");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Indexer name cannot be empty.", nameof(name));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            Indexer<TPrimary, TSecondary, TValue> indexer = new Indexer<TPrimary, TSecondary, TValue>(name, predicate);
            IEnumerable<KeyValuePair<Index<TPrimary, TSecondary>, Holder<TValue>>> entries = GetEntries();
            indexer.Rebuild(entries);
            predicateIndexers.Add(indexer.Name, indexer);
            return indexer;
        }

        public bool TryGetIndexer(string name, out Indexer<TPrimary, TSecondary, TValue> indexer)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Indexer name cannot be empty.", nameof(name));
            }

            return predicateIndexers.TryGetValue(name, out indexer);
        }

        public bool RemoveIndexer(string name)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Indexer name cannot be empty.", nameof(name));
            }

            return predicateIndexers.Remove(name);
        }

        public IReadOnlyCollection<TValue> GetIndexedValues(string name)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Indexer name cannot be empty.", nameof(name));
            }

            Indexer<TPrimary, TSecondary, TValue> indexer = predicateIndexers[name];
            return indexer.Values;
        }

        public bool Remove(TPrimary primary, TSecondary secondary)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            Index<TPrimary, TSecondary> index = new Index<TPrimary, TSecondary>(primary, secondary);
            return Remove(index);
        }

        public override bool Remove(Index<TPrimary, TSecondary> index)
        {
            if (predicateIndexers == null) throw new InvalidOperationException("Map indexers were not initialized.");
            if (!TryGetHolder(index, out Holder<TValue> holder)) return false;
            if (!base.Remove(index)) return false;
            foreach (Indexer<TPrimary, TSecondary, TValue> indexer in predicateIndexers.Values)
            {
                indexer.Untrack(holder);
            }
            return true;
        }

        public override void Clear()
        {
            base.Clear();
            foreach (Indexer<TPrimary, TSecondary, TValue> indexer in predicateIndexers.Values)
            {
                indexer.Clear();
            }
        }

        public IReadOnlyList<TValue> GetAll(TPrimary primary)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            return GetValueFromKey(primary, i => i.Primary);
        }

        public IReadOnlyList<TValue> GetAll(TSecondary secondary)
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            return GetValueFromKey(secondary, i => i.Secondary);
        }

        private IReadOnlyList<TValue> GetValueFromKey<T>(T key, Func<Index<TPrimary, TSecondary>, T> getter)
        {
            List<TValue> list = new List<TValue>();
            IEqualityComparer<T> comparer = EqualityComparer<T>.Default;
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, Holder<TValue>> entry in GetEntries())
            {
                T value = getter(entry.Key);
                if (comparer.Equals(value, key))
                {
                    list.Add(entry.Value.Value);
                }
            }
            return list;
        }

        public IReadOnlyCollection<TPrimary> GetPrimaryKeys()
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            HashSet<TPrimary> keys = new HashSet<TPrimary>(EqualityComparer<TPrimary>.Default);
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, Holder<TValue>> entry in GetEntries())
            {
                keys.Add(entry.Key.Primary);
            }

            return new List<TPrimary>(keys);
        }

        public IReadOnlyCollection<TSecondary> GetSecondaryKeys()
        {
            if (predicateIndexers == null)
            {
                throw new InvalidOperationException("Map indexers were not initialized.");
            }

            HashSet<TSecondary> keys = new HashSet<TSecondary>(EqualityComparer<TSecondary>.Default);
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, Holder<TValue>> entry in GetEntries())
            {
                keys.Add(entry.Key.Secondary);
            }

            return new List<TSecondary>(keys);
        }
    }
}
