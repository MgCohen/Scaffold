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
}