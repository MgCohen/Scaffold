using System.Collections.Generic;

namespace Scaffold.Maps
{
    public class Map<TPrimary, TSecondary, TValue> : BaseMap<Index<TPrimary, TSecondary>, TValue>
    {
        public Map() : base(new IndexComparer()) { }

        public TValue this[TPrimary primary] => this[primary, default];
        public TValue this[TSecondary secondary] => this[default, secondary];
        public TValue this[TPrimary primary, TSecondary secondary] => this[new Index<TPrimary, TSecondary>(primary, secondary)];

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
            var index = GetIndex(primary, secondary);
            Add(index, value);
        }

        public bool Contains(TPrimary primary, TSecondary secondary)
        {
            var index = GetIndex(primary, secondary);
            return ContainsKey(index);
        }

        public bool TryGetValue(TPrimary primary, TSecondary secondary, out TValue value)
        {
            var index = GetIndex(primary, secondary);
            return TryGetValue(index, out value);
        }

        private Index<TPrimary, TSecondary> GetIndex(TPrimary primary, TSecondary secondary)
        {
            return new Index<TPrimary, TSecondary>(primary, secondary);
        }
    }

    public class BaseMap<TKey, TValue> : Dictionary<TKey, TValue>
    {
        public BaseMap(IEqualityComparer<TKey> comparer) : base(comparer)
        {

        }

        protected TKey GetIndex()
        {
            return default;
        }
    }

    public class Map<TKey, TValue> : BaseMap<Index<TKey>, TValue>
    {
        public Map() : base(new IndexComparer()) { }

        public new void Add(Index<TKey> key, TValue value)
        {
            base.Add(key, value);
        }
    }

    public record Index<TPrimary, TSecondary> : Index<TPrimary>
    {

        public Index(TPrimary primary, TSecondary secondary) : base(primary)
        {
            this.primary = primary;
            this.secondary = secondary;
        }

        public TSecondary secondary;
    }

    public record Index<TPrimary> : IIndex
    {
        public Index(TPrimary primary)
        {
            this.primary = primary;
        }
        public TPrimary primary;
    }

    public class Indexer<TKey>
    {

    }

    public interface IIndex
    {

    }

    public class IndexComparer : IEqualityComparer<IIndex>
    {
        public bool Equals(IIndex x, IIndex y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(IIndex obj)
        {
            return obj.GetHashCode();
        }
    }

}