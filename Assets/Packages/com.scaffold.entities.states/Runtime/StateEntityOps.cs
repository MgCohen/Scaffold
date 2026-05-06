#nullable enable
using System.Collections.Generic;
using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public static class StateEntityOps
    {
        public static void RemoveModifiersFromSource(Store store, ModifierSource source)
        {
            var payloads = new List<object>();
            foreach ((Reference reference, EntityState state) in store.EnumerateAllPairs<EntityState>())
            {
                if (HasAnyModifierFromSource(state, source))
                {
                    payloads.Add(new RemoveModifiersBySourcePayload(reference, source));
                }
            }

            if (payloads.Count > 0)
            {
                store.ExecuteBatch(payloads);
            }
        }

        private static bool HasAnyModifierFromSource(EntityState state, ModifierSource source)
        {
            foreach (IReadOnlyList<ActiveModifier> bucket in state.ModifierStacks.Values)
            {
                for (int i = 0; i < bucket.Count; i++)
                {
                    var s = bucket[i].Source;
                    if (s.HasValue && s.Value.Equals(source))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
