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
            if (Lookup.ContainsKey(stateType))
            {
                return Lookup[stateType];
            }
            return Enumerable.Empty<ISubscription>();
        }

        public void Add(ISubscription sub)
        {
            Type type = sub.GetSubscriptionType();
            if (!Lookup.ContainsKey(type))
            {
                Lookup[type] = new List<ISubscription>();
            }
            Lookup[type].Add(sub);
        }

        public bool RemoveSubscription<TState>(Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            Type stateType = typeof(TState);
            if (!Lookup.TryGetValue(stateType, out List<ISubscription>? list))
            {
                return false;
            }

            int idx = FindLastMatchIndex(list, action);
            if (idx < 0) return false;
            list.RemoveAt(idx);
            if (list.Count == 0)
            {
                Lookup.Remove(stateType);
            }

            return true;
        }

        private int FindLastMatchIndex<TState>(List<ISubscription> list, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is TypedSubscription<TState> typed && typed.Matches(action))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
