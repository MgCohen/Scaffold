using System;
using System.Collections.Generic;

namespace LiveOps.Modules.GameData
{

    public static class ModulePrefetchKeys
    {
        public static string[]? UnionOrAll<T>(IEnumerable<T> modules, Func<T, string[]?> getKeys)
        {
            if (modules == null)
            {
                throw new ArgumentNullException(nameof(modules));
            }

            if (getKeys == null)
            {
                throw new ArgumentNullException(nameof(getKeys));
            }

            var distinct = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (T module in modules)
            {
                string[]? keys = getKeys(module);
                if (keys is null)
                {
                    return null;
                }

                foreach (string key in keys)
                {
                    if (seen.Add(key))
                    {
                        distinct.Add(key);
                    }
                }
            }

            return distinct.ToArray();
        }
    }
}
