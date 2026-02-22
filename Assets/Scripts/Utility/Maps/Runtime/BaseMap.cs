using System.Collections.Generic;

namespace Scaffold.Maps
{
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
}
