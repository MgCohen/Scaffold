#nullable enable
using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class RemoveModifierMutator : Mutator<EntityVariableState, RemoveModifierPayload>
    {
        public RemoveModifierMutator(EntityBridgeContext context)
        {
            this.context = context;
        }

        private readonly EntityBridgeContext context;

        public override EntityVariableState Change(EntityVariableState state, RemoveModifierPayload payload, IStateScope scope)
        {
            if (!context.TryGetDefinition(payload.EntityId, out IEntityDefinition? definition)) return state;
            if (!state.ModifierStacks.TryGetValue(payload.Variable, out IReadOnlyList<ActiveModifier>? existing) || existing == null) return state;

            int idx = FindModifierIndex(existing, payload.ModifierId);
            if (idx < 0) return state;

            var nextStacks = BuildStacksWithRemoval(state.ModifierStacks, payload.Variable, existing, idx);
            var nextEffective = EffectiveValueRecomputer.RecomputeFor(state.BaseValues, nextStacks, state.EffectiveValues, payload.Variable, definition);
            return state with { ModifierStacks = nextStacks, EffectiveValues = nextEffective };
        }

        private int FindModifierIndex(IReadOnlyList<ActiveModifier> bucket, ModifierId id)
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].Id.Equals(id)) return i;
            }

            return -1;
        }

        private static Dictionary<Variable, IReadOnlyList<ActiveModifier>> BuildStacksWithRemoval(IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> source, Variable variable, IReadOnlyList<ActiveModifier> bucket, int idx)
        {
            var nextStacks = EntityVariableState.CreateMutableStacks(source);
            var nextBucket = new List<ActiveModifier>(bucket);
            nextBucket.RemoveAt(idx);
            if (nextBucket.Count == 0)
            {
                nextStacks.Remove(variable);
            }
            else
            {
                nextStacks[variable] = nextBucket;
            }

            return nextStacks;
        }
    }
}
