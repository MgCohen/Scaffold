using System;
using System.Collections.Generic;

namespace Scaffold.Maps
{
    public class Indexer<TPrimary, TSecondary, TValue>
    {
        public Indexer(string name, Func<TPrimary, TSecondary, bool> predicate)
        {
            Name = ValidateName(name);
            this.predicate = ValidatePredicate(predicate);
            holders = new List<Holder<TValue>>();
        }

        public string Name { get; }

        public IReadOnlyCollection<TValue> Values
        {
            get
            {
                List<TValue> result = new List<TValue>(holders.Count);
                foreach (Holder<TValue> holder in holders)
{
    result.Add(holder.Value);
}
                return result;
            }
        }

        public int Count
        {
            get
            {
                return holders.Count;
            }
        }

        private readonly Func<TPrimary, TSecondary, bool> predicate;
        private readonly List<Holder<TValue>> holders;

        internal void Rebuild(IEnumerable<KeyValuePair<Index<TPrimary, TSecondary>, Holder<TValue>>> entries)
        {
            holders.Clear();
            foreach (KeyValuePair<Index<TPrimary, TSecondary>, Holder<TValue>> entry in entries)
{
    Track(entry.Key, entry.Value);
}
        }

        internal void Track(Index<TPrimary, TSecondary> index, Holder<TValue> holder)
        {
            bool isMatch = predicate(index.Primary, index.Secondary);
            if (isMatch)
            {
                if (holders.Contains(holder) == false)
                {
                    holders.Add(holder);
                }
            }
            else
            {
                holders.Remove(holder);
            }
        }

        internal void Untrack(Holder<TValue> holder)
        {
            holders.Remove(holder);
        }

        internal void Clear()
        {
            holders.Clear();
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

        private Func<TPrimary, TSecondary, bool> ValidatePredicate(Func<TPrimary, TSecondary, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
            return predicate;
        }
    }
}

