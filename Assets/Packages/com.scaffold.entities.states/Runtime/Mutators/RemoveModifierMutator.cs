#nullable enable
using System.Collections.Immutable;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class RemoveModifierMutator : Mutator<EntityVariableState, RemoveModifierPayload>
    {
        private readonly EntityBridgeContext context;

        public RemoveModifierMutator(EntityBridgeContext context)
        {
            this.context = context;
        }

        public override EntityVariableState Change(
            EntityVariableState state,
            RemoveModifierPayload payload,
            IStateScope scope)
        {
            if (!context.TryGetDefinition(payload.EntityId, out var definition))
            {
                return state;
            }

            if (!state.ModifierStacks.TryGetValue(payload.Variable, out var bucket))
            {
                return state;
            }

            int idx = -1;
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].Id.Equals(payload.ModifierId))
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0)
            {
                return state;
            }

            var nextBucket = bucket.RemoveAt(idx);
            var nextStacks = nextBucket.Count == 0
                ? state.ModifierStacks.Remove(payload.Variable)
                : state.ModifierStacks.SetItem(payload.Variable, nextBucket);

            var nextEffective = EffectiveValueRecomputer.RecomputeFor(
                state,
                nextStacks,
                payload.Variable,
                definition);

            return state with { ModifierStacks = nextStacks, EffectiveValues = nextEffective };
        }
    }
}
