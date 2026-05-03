using System;
using System.Collections.Generic;
using System.Linq;

namespace Scaffold.States
{
    internal class Ledger
    {
        public Dictionary<Type, List<ISubscription>> Lookup = new();

        public IEnumerable<ISubscription> Get(Type stateType)
        {
            if (Lookup.TryGetValue(stateType, out var list))
            {
                return list;
            }

            return Enumerable.Empty<ISubscription>();
        }

        public void Add(ISubscription sub)
        {
            Type type = sub.GetSubscriptionType();
            if (!Lookup.TryGetValue(type, out var list))
            {
                list = new List<ISubscription>();
                Lookup[type] = list;
            }

            list.Add(sub);
        }

        public bool RemoveSubscription<TState>(object removalKey) where TState : BaseState
        {
            Type stateType = typeof(TState);
            if (!Lookup.TryGetValue(stateType, out List<ISubscription>? list))
            {
                return false;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is TypedSubscription<TState> typed && typed.MatchesRemoval(removalKey))
                {
                    list.RemoveAt(i);
                    if (list.Count == 0)
                    {
                        Lookup.Remove(stateType);
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
