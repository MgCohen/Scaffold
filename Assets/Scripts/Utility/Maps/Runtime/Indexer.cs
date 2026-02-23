using System;
using System.Collections.Generic;

namespace Scaffold.Maps
{
    public class Indexer<TPrimary, TSecondary, TValue>
    {
        public Indexer(string name, Func<TPrimary, TSecondary, TValue, bool> predicate)
        {
            Name = ValidateName(name);
            this.predicate = ValidatePredicate(predicate);
            values = new Dictionary<Index<TPrimary, TSecondary>, TValue>();
        }

        private readonly Func<TPrimary, TSecondary, TValue, bool> predicate;
        private readonly Dictionary<Index<TPrimary, TSecondary>, TValue> values;

        public string Name { get; }
        public IReadOnlyCollection<TValue> Values
        {
            get
            {
                return values.Values;
            }
        }

        public int Count
        {
            get
            {
                return values.Count;
            }
        }

        internal void Rebuild(IEnumerable<KeyValuePair<Index<TPrimary, TSecondary>, TValue>> entries)
        {
            values.Clear();
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, TValue> entry in entries)
            {
                Track(entry.Key, entry.Value);
            }
        }

        internal void Track(Index<TPrimary, TSecondary> index, TValue value)
        {
            bool isMatch = Matches(index, value);
            if (isMatch)
            {
                values[index] = value;
            }
            if (isMatch == false)
            {
                values.Remove(index);
            }
        }

        internal void Untrack(Index<TPrimary, TSecondary> index)
        {
            values.Remove(index);
        }

        internal void Clear()
        {
            values.Clear();
        }

        private bool Matches(Index<TPrimary, TSecondary> index, TValue value)
        {
            return predicate(index.primary, index.secondary, value);
        }

        private string ValidateName(string name)
        {
            bool isValid = string.IsNullOrWhiteSpace(name) == false;
            if (isValid == false)
            {
                throw new ArgumentException("Indexer name cannot be null or empty.", nameof(name));
            }
            return name;
        }

        private Func<TPrimary, TSecondary, TValue, bool> ValidatePredicate(Func<TPrimary, TSecondary, TValue, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
            return predicate;
        }
    }
}
