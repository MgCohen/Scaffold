namespace Scaffold.Maps
{
    public class Map<TKey, TValue> : BaseMap<Index<TKey>, TValue>
    {
        public Map() : base(new IndexComparer())
        {
        }

        public new void Add(Index<TKey> key, TValue value)
        {
            base.Add(key, value);
        }
    }
}
