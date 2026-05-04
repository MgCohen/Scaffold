using System;
using System.Collections;
using System.Collections.Generic;

namespace Scaffold.Maps
{
    public class BaseMap<TKey, TValue> : IReadOnlyBaseMap<TKey, TValue>
    {
        protected BaseMap()
        {
            data = new Dictionary<TKey, TValue>();
        }

        public BaseMap(IEqualityComparer<TKey> comparer)
        {
            if (comparer is null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            data = new Dictionary<TKey, TValue>(comparer);
        }

        public virtual TValue this[TKey key]
        {
            get => data[key];
            set => data[key] = value;
        }

        public int Count => data.Count;

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (KeyValuePair<TKey, TValue> entry in data)
                {
                    yield return entry.Value;
                }
            }
        }

        private readonly Dictionary<TKey, TValue> data;

        public bool ContainsKey(TKey key) => data.ContainsKey(key);

        public bool TryGetValue(TKey key, out TValue value) => data.TryGetValue(key, out value);

        public virtual bool Remove(TKey key) => data.Remove(key);

        public virtual void Clear() => data.Clear();

        protected void Add(TKey key, TValue value)
        {
            data.Add(key, value);
        }

        internal IEnumerable<KeyValuePair<TKey, TValue>> GetEntries()
        {
            return data;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (KeyValuePair<TKey, TValue> entry in data)
            {
                yield return entry;
            }
        }
    }
}
