using System;
using System.Collections.Generic;

namespace Scaffold.Maps
{
    /// <summary>
    /// Predicate-based filtered view over map keys. Predicates filter by primary/secondary keys only — not by value;
    /// mutating <c>map[index]</c> does not move entries in or out of the indexer set.
    /// </summary>
    public sealed class Indexer<TPrimary, TSecondary, TValue> : IReadOnlyIndexer<TPrimary, TSecondary, TValue>
    {
        private readonly Map<TPrimary, TSecondary, TValue> owner;
        private readonly Func<TPrimary, TSecondary, bool> keyPredicate;
        private readonly HashSet<Index<TPrimary, TSecondary>> tracked;

        internal Indexer(
            Map<TPrimary, TSecondary, TValue> owner,
            string name,
            Func<TPrimary, TSecondary, bool> keyPredicate,
            IEqualityComparer<Index<TPrimary, TSecondary>> indexComparer)
        {
            this.owner = owner;
            Name = ValidateName(name);
            this.keyPredicate = ValidateKeyPredicate(keyPredicate);
            tracked = new HashSet<Index<TPrimary, TSecondary>>(indexComparer);
        }

        internal static Indexer<TPrimary, TSecondary, TValue> CreateBound(
            Map<TPrimary, TSecondary, TValue> owner,
            string name,
            Func<TPrimary, TSecondary, bool> keyPredicate)
        {
            IEqualityComparer<Index<TPrimary, TSecondary>> comparer =
                EqualityComparer<Index<TPrimary, TSecondary>>.Default;
            return new Indexer<TPrimary, TSecondary, TValue>(owner, name, keyPredicate, comparer);
        }

        public string Name { get; }

        /// <inheritdoc cref="TrackedCount"/>
        public int Count => TrackedCount;

        internal int TrackedCount => tracked.Count;

        /// <summary>Lazy key-ordered view; <see cref="IndexerValuesView{TPrimary,TSecondary,TValue}.Count"/> is O(1).</summary>
        public IndexerValuesView<TPrimary, TSecondary, TValue> Values =>
            new IndexerValuesView<TPrimary, TSecondary, TValue>(this);

        internal void Rebuild(IEnumerable<KeyValuePair<Index<TPrimary, TSecondary>, TValue>> entries)
        {
            tracked.Clear();
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, TValue> entry in entries)
            {
                Track(entry.Key);
            }
        }

        internal void Track(Index<TPrimary, TSecondary> index)
        {
            if (keyPredicate(index.Primary, index.Secondary))
            {
                tracked.Add(index);
            }
            else
            {
                tracked.Remove(index);
            }
        }

        internal void Untrack(Index<TPrimary, TSecondary> index)
        {
            tracked.Remove(index);
        }

        internal void Clear()
        {
            tracked.Clear();
        }

        internal HashSet<Index<TPrimary, TSecondary>>.Enumerator GetTrackedKeyEnumerator()
        {
            return tracked.GetEnumerator();
        }

        internal bool OwnerTryGetValue(Index<TPrimary, TSecondary> index, out TValue value)
        {
            return owner.TryGetValue(index, out value);
        }

        internal IReadOnlyList<TValue> SnapshotValues()
        {
            if (tracked.Count == 0)
            {
                return Array.Empty<TValue>();
            }

            List<TValue> list = new List<TValue>(tracked.Count);
            foreach (Index<TPrimary, TSecondary> index in tracked)
            {
                if (owner.TryGetValue(index, out TValue value))
                {
                    list.Add(value);
                }
            }

            return list;
        }

        private string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Indexer name cannot be null or empty.", nameof(name));
            }

            return name;
        }

        private Func<TPrimary, TSecondary, bool> ValidateKeyPredicate(Func<TPrimary, TSecondary, bool> keyPredicate)
        {
            if (keyPredicate is null)
            {
                throw new ArgumentNullException(nameof(keyPredicate));
            }

            return keyPredicate;
        }
    }
}
