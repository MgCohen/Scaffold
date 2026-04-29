#nullable enable
using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class SetBaseValueMutator : Mutator<EntityVariableState, SetBaseValuePayload>
    {
        public SetBaseValueMutator(EntityBridgeContext context)
        {
            this.context = context;
        }

        private readonly EntityBridgeContext context;

        public override EntityVariableState Change(EntityVariableState state, SetBaseValuePayload payload, IStateScope scope)
        {
            if (!context.TryGetDefinition(payload.EntityId, out IEntityDefinition? definition)) return state;

            var nextBases = EntityVariableState.CreateNewBaseDictionary(state.BaseValues);
            nextBases[payload.Variable] = payload.Value;
            var nextStacks = EntityVariableState.CreateNewModifierStacksDictionary(state.ModifierStacks);
            Dictionary<Variable, VariableValue> nextEffective = EffectiveValueRecomputer.RecomputeFor(nextBases, nextStacks, state.EffectiveValues, payload.Variable, definition);
            return new EntityVariableState(nextBases, nextStacks, nextEffective);
        }
    }
}
