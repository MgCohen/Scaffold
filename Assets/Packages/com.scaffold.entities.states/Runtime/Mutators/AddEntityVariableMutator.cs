#nullable enable

using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class AddEntityVariableMutator : Mutator<EntityVariableState, AddEntityVariablePayload>
    {
        public override EntityVariableState Change(EntityVariableState state, AddEntityVariablePayload payload, IStateScope scope)
        {
            return state.WithVariable(payload.Variable, payload.InitialValue);
        }
    }
}
