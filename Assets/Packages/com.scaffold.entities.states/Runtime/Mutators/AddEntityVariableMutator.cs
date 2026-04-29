#nullable enable
using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class AddEntityVariableMutator : Mutator<EntityVariableState, AddEntityVariablePayload>
    {
        private readonly EntityBridgeContext context;

        public AddEntityVariableMutator(EntityBridgeContext context)
        {
            this.context = context;
        }

        public override EntityVariableState Change(
            EntityVariableState state,
            AddEntityVariablePayload payload,
            IStateScope scope)
        {
            if (!context.TryGetDefinition(payload.EntityId, out var definition))
            {
                return state;
            }

            if (state.BaseValues.ContainsKey(payload.Variable))
            {
                return state;
            }

            if (definition.TryGetDefaultValue(payload.Variable, out _))
            {
                return state;
            }

            var nextBaseValues = state.BaseValues.Add(payload.Variable, payload.InitialValue);
            return state with { BaseValues = nextBaseValues };
        }
    }
}
