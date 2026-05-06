#nullable enable

using Scaffold.States;

namespace Scaffold.Entities.States
{
    [Mutator]
    internal sealed class SetBaseValueMutator : Mutator<EntityState, SetBaseValuePayload>
    {
        public override EntityState Change(EntityState state, SetBaseValuePayload payload, IStateScope scope)
        {
            return state.WithBaseValue(payload.Variable, payload.Value);
        }
    }
}
