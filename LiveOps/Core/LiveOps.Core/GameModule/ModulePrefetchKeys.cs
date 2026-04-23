using System;
using System.Collections.Generic;

namespace LiveOps.Core.GameModule
{
    /// <summary>
    /// Unions prefetch key hints from modules. Any <c>null</c> from a module means warm full snapshot; otherwise distinct keys are merged.
    /// </summary>
    public static class ModulePrefetchKeys
    {
        public static string[]? UnionOrAll(IEnumerable<IGameModule> modules, Func<IGameModule, string[]?> pick)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
            foreach (IGameModule m in modules)
            {
                if (m == null)
                {
                    continue;
                }

                string[]? k = pick(m);
                if (k == null)
                {
                    return null;
                }

                for (int i = 0; i < k.Length; i++)
                {
                    set.Add(k[i]);
                }
            }

            return set.Count == 0 ? Array.Empty<string>() : new List<string>(set).ToArray();
        }
    }
}
