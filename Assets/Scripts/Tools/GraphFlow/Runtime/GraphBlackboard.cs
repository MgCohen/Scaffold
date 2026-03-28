using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class GraphBlackboard
    {
        readonly Dictionary<string, object> values = new Dictionary<string, object>();

        public void Set(string key, object value) => values[key] = value;

        public T Get<T>(string key, T defaultValue = default)
        {
            if (!values.TryGetValue(key, out var o))
                return defaultValue;
            if (o is T t)
                return t;
            try
            {
                return (T)Convert.ChangeType(o, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        internal IEnumerable<KeyValuePair<string, object>> Enumerate() => values;
    }
}
