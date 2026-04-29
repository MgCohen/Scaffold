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
            if (store == null)
            {
                throw new System.ArgumentNullException(nameof(store));
            }

            List<object> payloads = BuildRemoveModifiersBySourcePayloads(store, source);
            if (payloads.Count > 0)
            {
                store.ExecuteBatch(payloads);
            }
        }

        private static List<object> BuildRemoveModifiersBySourcePayloads(Store store, ModifierSource source)
        {
            var payloads = new List<object>();
            foreach ((IReference reference, EntityVariableState state) in store.EnumerateAll<EntityVariableState>())
            {
                TryAppendPayloadForSlice(reference, state, source, payloads);
            }

            return payloads;
        }

        private static void TryAppendPayloadForSlice(IReference reference, EntityVariableState state, ModifierSource source, List<object> payloads)
        {
            if (reference is not InstanceId entityId)
            {
                return;
            }

            if (!HasAnyModifierFromSource(state, source))
            {
                return;
            }

            payloads.Add(new RemoveModifiersBySourcePayload(entityId, source));
        }

        private static bool HasAnyModifierFromSource(EntityVariableState state, ModifierSource source)
        {
            foreach (IReadOnlyList<ActiveModifier> bucket in state.ModifierStacks.Values)
            {
                if (BucketContainsSource(bucket, source))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BucketContainsSource(IReadOnlyList<ActiveModifier> bucket, ModifierSource source)
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                ActiveModifier am = bucket[i];
                if (am.Source.HasValue && am.Source.Value.Equals(source))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
