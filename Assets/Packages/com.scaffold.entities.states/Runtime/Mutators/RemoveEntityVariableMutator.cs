#nullable enable

using Scaffold.States;

namespace Scaffold.Entities.States
{
    [Mutator]
    internal sealed class RemoveEntityVariableMutator : Mutator<EntityVariableState, RemoveEntityVariablePayload>
    {
        public override EntityVariableState Change(EntityVariableState state, RemoveEntityVariablePayload payload, IStateScope scope)
        {
            return state.WithoutVariable(payload.Variable);
        }
    }
}
