namespace Scaffold.Maps
{
    public class Map<TKey, TValue> : BaseMap<Index<TKey>, TValue>
    {
        public Map() : base()
        {
        }

        public TValue this[TKey key]
        {
            get
            {
                Index<TKey> index = CreateIndex(key);
                return this[index];
            }
        }

        public new void Add(Index<TKey> key, TValue value)
        {
            base.Add(key, value);
        }

        public void Add(TKey key, TValue value)
        {
            Index<TKey> index = CreateIndex(key);
            Add(index, value);
        }

        public bool Contains(TKey key)
        {
            Index<TKey> index = CreateIndex(key);
            return ContainsKey(index);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            Index<TKey> index = CreateIndex(key);
            return TryGetValue(index, out value);
        }

        public bool Remove(TKey key)
        {
            Index<TKey> index = CreateIndex(key);
            return Remove(index);
        }

        private Index<TKey> CreateIndex(TKey key)
        {
            return new Index<TKey>(key);
        }
    }
}
