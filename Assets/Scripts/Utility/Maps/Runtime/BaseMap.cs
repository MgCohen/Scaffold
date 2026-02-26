using System;
using System.Collections;
using System.Collections.Generic;

namespace Scaffold.Maps
{
    public class BaseMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly Dictionary<TKey, Holder<TValue>> data;

        protected BaseMap()
        {
            data = new Dictionary<TKey, Holder<TValue>>();
        }

        public BaseMap(IEqualityComparer<TKey> comparer)
        {
            data = new Dictionary<TKey, Holder<TValue>>(comparer);
        }

        public int Count
        {
            get
            {
                return data.Count;
            }
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (KeyValuePair<TKey, Holder<TValue>> entry in data)
                {
                    yield return entry.Value.Value;
                }
            }
        }

        public virtual TValue this[TKey key]
        {
            get
            {
                return data[key].Value;
            }
            set
            {
                data[key].Value = value;
            }
        }

        public bool ContainsKey(TKey key)
        {
            return data.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            bool found = data.TryGetValue(key, out Holder<TValue> holder);
            value = found ? holder.Value : default;
            return found;
        }

        public virtual bool Remove(TKey key)
        {
            return data.Remove(key);
        }

        public virtual void Clear()
        {
            data.Clear();
        }

        protected void Add(TKey key, Holder<TValue> holder)
        {
            data.Add(key, holder);
        }

        internal bool TryGetHolder(TKey key, out Holder<TValue> holder)
        {
            return data.TryGetValue(key, out holder);
        }

        internal IEnumerable<KeyValuePair<TKey, Holder<TValue>>> GetEntries()
        {
            return data;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (KeyValuePair<TKey, Holder<TValue>> entry in data)
            {
                yield return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
