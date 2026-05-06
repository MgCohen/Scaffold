#nullable enable

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    [Mutator]
    internal sealed class AddModifierMutator : Mutator<EntityState, AddModifierPayload>
    {
        public override EntityState Change(EntityState state, AddModifierPayload payload, IStateScope scope)
        {
            return state.WithModifier(payload.Variable, new ActiveModifier(payload.ModifierId, payload.Modifier, payload.Source));
        }
    }
}
