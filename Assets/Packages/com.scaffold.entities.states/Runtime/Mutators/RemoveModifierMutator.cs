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
            if (!state.ModifierStacks.TryGetValue(payload.Variable, out List<ActiveModifier>? bucket) || bucket == null) return state;

            int idx = bucket.FindIndex(m => m.Id.Equals(payload.ModifierId));
            if (idx < 0) return state;

            var nextBases = EntityVariableState.CreateNewBaseDictionary(state.BaseValues);
            var nextStacks = EntityVariableState.CreateNewModifierStacksDictionary(state.ModifierStacks);
            var nextBucket = new List<ActiveModifier>(nextStacks[payload.Variable]);
            nextBucket.RemoveAt(idx);
            ReplaceOrClear(nextStacks, payload.Variable, nextBucket);

            return new EntityVariableState(nextBases, nextStacks, EffectiveValueRecomputer.RecomputeFor(nextBases, nextStacks, state.EffectiveValues, payload.Variable, definition));
        }

        private void ReplaceOrClear(Dictionary<Variable, List<ActiveModifier>> nextStacks, Variable variable, List<ActiveModifier> nextBucket)
        {
            if (nextBucket.Count == 0)
            {
                nextStacks.Remove(variable);
            }
            else
            {
                nextStacks[variable] = nextBucket;
            }
        }
    }
}
