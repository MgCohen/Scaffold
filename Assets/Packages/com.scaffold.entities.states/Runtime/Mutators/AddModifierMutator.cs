#nullable enable
using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class AddModifierMutator : Mutator<EntityVariableState, AddModifierPayload>
    {
        public AddModifierMutator(EntityBridgeContext context)
        {
            this.context = context;
        }

        private readonly EntityBridgeContext context;

        public override EntityVariableState Change(EntityVariableState state, AddModifierPayload payload, IStateScope scope)
        {
            if (!context.TryGetDefinition(payload.EntityId, out IEntityDefinition? definition)) return state;

            var nextBases = EntityVariableState.CreateNewBaseDictionary(state.BaseValues);
            var nextStacks = EntityVariableState.CreateNewModifierStacksDictionary(state.ModifierStacks);
            List<ActiveModifier> bucket = nextStacks.TryGetValue(payload.Variable, out List<ActiveModifier>? oldBucket) ? new List<ActiveModifier>(oldBucket!) : new List<ActiveModifier>();
            int insertAt = 0;
            while (insertAt < bucket.Count && bucket[insertAt].Modifier.Order <= payload.Modifier.Order)
            {
                insertAt++;
            }

            bucket.Insert(insertAt, new ActiveModifier(payload.ModifierId, payload.Modifier));
            nextStacks[payload.Variable] = bucket;
            return new EntityVariableState(nextBases, nextStacks, EffectiveValueRecomputer.RecomputeFor(nextBases, nextStacks, state.EffectiveValues, payload.Variable, definition));
        }
    }
}
