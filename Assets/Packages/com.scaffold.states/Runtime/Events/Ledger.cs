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
    }
}
