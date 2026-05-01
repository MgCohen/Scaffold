#nullable enable

using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class SetBaseValueMutator : Mutator<EntityVariableState, SetBaseValuePayload>
    {
        public override EntityVariableState Change(EntityVariableState state, SetBaseValuePayload payload, IStateScope scope)
        {
            return state.WithBaseValue(payload.Variable, payload.Value);
        }
    }
}
