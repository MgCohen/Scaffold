#nullable enable

using Scaffold.States;

namespace Scaffold.Entities.States
{
    [Mutator]
    internal sealed class AddEntityVariableMutator : Mutator<EntityState, AddEntityVariablePayload>
    {
        public override EntityState Change(EntityState state, AddEntityVariablePayload payload, IStateScope scope)
        {
            return state.WithVariable(payload.Variable, payload.InitialValue);
        }
    }
}
