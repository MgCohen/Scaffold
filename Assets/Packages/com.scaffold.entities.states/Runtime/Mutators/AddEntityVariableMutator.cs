#nullable enable
using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class AddEntityVariableMutator : Mutator<EntityVariableState, AddEntityVariablePayload>
    {
        public AddEntityVariableMutator(EntityBridgeContext context)
        {
            this.context = context;
        }

        private readonly EntityBridgeContext context;

        public override EntityVariableState Change(EntityVariableState state, AddEntityVariablePayload payload, IStateScope scope)
        {
            if (!context.TryGetDefinition(payload.EntityId, out IEntityDefinition? definition)) return state;
            if (state.BaseValues.ContainsKey(payload.Variable)) return state;
            if (definition.TryGetDefaultValue(payload.Variable, out _)) return state;

            var nextBases = EntityVariableState.CreateMutableValues(state.BaseValues);
            nextBases.Add(payload.Variable, payload.InitialValue);
            return state with { BaseValues = nextBases };
        }
    }
}
