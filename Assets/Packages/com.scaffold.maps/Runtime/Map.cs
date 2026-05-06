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

        /// <inheritdoc />
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
                if (TryGetValue(index, out _))
                {
                    base[index] = value;
                    return;
                }

                Add(index, value);
            }
        }

        /// <inheritdoc />
        public override TValue this[Index<TPrimary, TSecondary> index]
        {
            get => base[index];
            set
            {
                if (TryGetValue(index, out _))
                {
                    base[index] = value;
                    return;
                }

                Add(index, value);
            }
        }

        private readonly Dictionary<string, Indexer<TPrimary, TSecondary, TValue>> predicateIndexers;

        /// <inheritdoc />
        public void Add(TPrimary primary, TSecondary secondary, TValue value)
        {
            Index<TPrimary, TSecondary> index = new Index<TPrimary, TSecondary>(primary, secondary);
            Add(index, value);
        }

        /// <inheritdoc />
        public void Add(Index<TPrimary, TSecondary> index, TValue value)
        {
            base.Add(index, value);
            foreach (Indexer<TPrimary, TSecondary, TValue> indexer in predicateIndexers.Values)
            {
                indexer.Track(index);
            }
        }

        /// <inheritdoc />
        public bool Contains(TPrimary primary, TSecondary secondary)
        {
            Index<TPrimary, TSecondary> index = new Index<TPrimary, TSecondary>(primary, secondary);
            return ContainsKey(index);
        }

        /// <inheritdoc />
        public bool TryGetValue(TPrimary primary, TSecondary secondary, out TValue value)
        {
            Index<TPrimary, TSecondary> index = new Index<TPrimary, TSecondary>(primary, secondary);
            return TryGetValue(index, out value);
        }

        /// <summary>
        /// Registers a named indexer. The predicate evaluates only composite keys — not stored values —
        /// so value updates do not reclassify indexer membership.
        /// </summary>
        public Indexer<TPrimary, TSecondary, TValue> AddIndexer(string name, Func<TPrimary, TSecondary, bool> keyPredicate)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Indexer name cannot be empty.", nameof(name));
            }

            if (keyPredicate is null)
            {
                throw new ArgumentNullException(nameof(keyPredicate));
            }

            if (predicateIndexers.ContainsKey(name))
            {
                throw new InvalidOperationException($"Indexer '{name}' already exists.");
            }

            Indexer<TPrimary, TSecondary, TValue> indexer =
                Indexer<TPrimary, TSecondary, TValue>.CreateBound(this, name, keyPredicate);
            indexer.Rebuild(GetEntries());
            predicateIndexers.Add(indexer.Name, indexer);
            return indexer;
        }

        /// <inheritdoc />
        public bool TryGetIndexer(string name, out IReadOnlyIndexer<TPrimary, TSecondary, TValue> indexer)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Indexer name cannot be empty.", nameof(name));
            }

            bool found = predicateIndexers.TryGetValue(name, out Indexer<TPrimary, TSecondary, TValue> concrete);
            indexer = concrete;
            return found;
        }

        /// <inheritdoc />
        public bool RemoveIndexer(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Indexer name cannot be empty.", nameof(name));
            }

            return predicateIndexers.Remove(name);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<TValue> GetIndexedValues(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Indexer name cannot be empty.", nameof(name));
            }

            if (!predicateIndexers.TryGetValue(name, out Indexer<TPrimary, TSecondary, TValue> indexer))
            {
                throw new KeyNotFoundException($"Indexer '{name}' not registered. Call AddIndexer first.");
            }

            return indexer.SnapshotValues();
        }

        /// <inheritdoc />
        public bool Remove(TPrimary primary, TSecondary secondary)
        {
            Index<TPrimary, TSecondary> index = new Index<TPrimary, TSecondary>(primary, secondary);
            return Remove(index);
        }

        /// <inheritdoc />
        public override bool Remove(Index<TPrimary, TSecondary> index)
        {
            if (!ContainsKey(index))
            {
                return false;
            }

            if (!base.Remove(index))
            {
                return false;
            }

            foreach (Indexer<TPrimary, TSecondary, TValue> indexer in predicateIndexers.Values)
            {
                indexer.Untrack(index);
            }

            return true;
        }

        /// <inheritdoc />
        public override void Clear()
        {
            base.Clear();
            foreach (Indexer<TPrimary, TSecondary, TValue> indexer in predicateIndexers.Values)
            {
                indexer.Clear();
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<KeyValuePair<TSecondary, TValue>> GetAll(TPrimary primary)
        {
            List<KeyValuePair<TSecondary, TValue>> list = new List<KeyValuePair<TSecondary, TValue>>();
            IEqualityComparer<TPrimary> comparer = EqualityComparer<TPrimary>.Default;
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, TValue> entry in GetEntries())
            {
                if (comparer.Equals(entry.Key.Primary, primary))
                {
                    list.Add(new KeyValuePair<TSecondary, TValue>(entry.Key.Secondary, entry.Value));
                }
            }

            return list;
        }

        /// <inheritdoc />
        public IReadOnlyList<KeyValuePair<TPrimary, TValue>> GetAll(TSecondary secondary)
        {
            List<KeyValuePair<TPrimary, TValue>> list = new List<KeyValuePair<TPrimary, TValue>>();
            IEqualityComparer<TSecondary> comparer = EqualityComparer<TSecondary>.Default;
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, TValue> entry in GetEntries())
            {
                if (comparer.Equals(entry.Key.Secondary, secondary))
                {
                    list.Add(new KeyValuePair<TPrimary, TValue>(entry.Key.Primary, entry.Value));
                }
            }

            return list;
        }

        /// <inheritdoc />
        public void GetAll(TSecondary secondary, ICollection<TValue> results)
        {
            if (results is null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            ForEachMatchingSecondary(secondary, (_, v) => results.Add(v));
        }

        internal void AddPrimaryKeysForSecondary(TSecondary secondary, ICollection<TPrimary> primaryKeys)
        {
            if (primaryKeys is null)
            {
                throw new ArgumentNullException(nameof(primaryKeys));
            }

            ForEachMatchingSecondary(secondary, (index, _) => primaryKeys.Add(index.Primary));
        }

        /// <inheritdoc />
        public void GetAll(TPrimary primary, ICollection<TValue> results)
        {
            if (results is null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            ForEachMatchingPrimary(primary, (_, v) => results.Add(v));
        }

        private void ForEachMatchingPrimary(TPrimary primary, Action<Index<TPrimary, TSecondary>, TValue> onMatch)
        {
            IEqualityComparer<TPrimary> comparer = EqualityComparer<TPrimary>.Default;
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, TValue> entry in GetEntries())
            {
                if (comparer.Equals(entry.Key.Primary, primary))
                {
                    onMatch(entry.Key, entry.Value);
                }
            }
        }

        private void ForEachMatchingSecondary(TSecondary secondary, Action<Index<TPrimary, TSecondary>, TValue> onMatch)
        {
            IEqualityComparer<TSecondary> comparer = EqualityComparer<TSecondary>.Default;
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, TValue> entry in GetEntries())
            {
                if (comparer.Equals(entry.Key.Secondary, secondary))
                {
                    onMatch(entry.Key, entry.Value);
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyCollection<TPrimary> GetPrimaryKeys()
        {
            HashSet<TPrimary> keys = new HashSet<TPrimary>(EqualityComparer<TPrimary>.Default);
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, TValue> entry in GetEntries())
            {
                keys.Add(entry.Key.Primary);
            }

            return keys;
        }

        /// <inheritdoc />
        public IReadOnlyCollection<TSecondary> GetSecondaryKeys()
        {
            HashSet<TSecondary> keys = new HashSet<TSecondary>(EqualityComparer<TSecondary>.Default);
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, TValue> entry in GetEntries())
            {
                keys.Add(entry.Key.Secondary);
            }

            return keys;
        }
    }
}
