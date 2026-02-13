using System;
using System.Collections.Generic;
using System.Linq;

namespace Scaffold.States
{
    internal class Ledger
    {
        public Dictionary<Type, List<ISubscription>> lookup = new();

        public IEnumerable<ISubscription> Get(Type stateType)
        {
            if (lookup.ContainsKey(stateType))
            {
                return lookup[stateType];
            }
            return Enumerable.Empty<ISubscription>();
        }

        public void Add(ISubscription sub)
        {
            Type type = sub.GetSubscriptionType();
            if (!lookup.ContainsKey(type))
            {
                lookup[type] = new List<ISubscription>();
            }
            lookup[type].Add(sub);
        }
    }
}