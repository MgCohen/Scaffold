#nullable enable

using Scaffold.States;

namespace Scaffold.Entities.States
{
    [Mutator]
    internal sealed class RemoveModifierMutator : Mutator<EntityState, RemoveModifierPayload>
    {
        public override EntityState Change(EntityState state, RemoveModifierPayload payload, IStateScope scope)
        {
            return state.WithoutModifier(payload.Variable, payload.ModifierId);
        }
    }
}
