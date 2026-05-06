#nullable enable

using Scaffold.States;

namespace Scaffold.Entities.States
{
    [Mutator]
    internal sealed class RemoveEntityVariableMutator : Mutator<EntityState, RemoveEntityVariablePayload>
    {
        public override EntityState Change(EntityState state, RemoveEntityVariablePayload payload, IStateScope scope)
        {
            return state.WithoutVariable(payload.Variable);
        }
    }
}
