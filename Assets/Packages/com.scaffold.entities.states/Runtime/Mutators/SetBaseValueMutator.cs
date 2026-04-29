#nullable enable
using System.Collections.Immutable;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class SetBaseValueMutator : Mutator<EntityVariableState, SetBaseValuePayload>
    {
        private readonly EntityBridgeContext context;

        public SetBaseValueMutator(EntityBridgeContext context)
        {
            this.context = context;
        }

        public override EntityVariableState Change(
            EntityVariableState state,
            SetBaseValuePayload payload,
            IStateScope scope)
        {
            if (!context.TryGetDefinition(payload.EntityId, out var definition))
            {
                return state;
            }

            var nextBaseValues = state.BaseValues.SetItem(payload.Variable, payload.Value);
            var stateWithBase = state with { BaseValues = nextBaseValues };
            var nextEffective = EffectiveValueRecomputer.RecomputeFor(
                stateWithBase,
                stateWithBase.ModifierStacks,
                payload.Variable,
                definition);
            return stateWithBase with { EffectiveValues = nextEffective };
        }
    }
}
