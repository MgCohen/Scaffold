#nullable enable
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

            var nextBases = EntityVariableState.CreateMutableValues(state.BaseValues);
            nextBases[payload.Variable] = payload.Value;

            var nextEffective = EffectiveValueRecomputer.RecomputeFor(nextBases, state.ModifierStacks, state.EffectiveValues, payload.Variable, definition);
            return state with { BaseValues = nextBases, EffectiveValues = nextEffective };
        }
    }
}
