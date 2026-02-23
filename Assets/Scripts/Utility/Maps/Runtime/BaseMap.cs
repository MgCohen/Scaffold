using System.Collections.Generic;

namespace Scaffold.Maps
{
    public class BaseMap<TKey, TValue> : Dictionary<TKey, TValue>
    {
        protected BaseMap()
        {
        }

        public BaseMap(IEqualityComparer<TKey> comparer) : base(comparer)
        {
        }
    }
}
