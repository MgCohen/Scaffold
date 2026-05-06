#nullable enable

using Scaffold.States;

namespace Scaffold.Entities.States
{
    [Mutator]
    internal sealed class RemoveModifiersBySourceMutator : Mutator<EntityState, RemoveModifiersBySourcePayload>
    {
        public override EntityState Change(EntityState state, RemoveModifiersBySourcePayload payload, IStateScope scope)
        {
            return state.WithoutModifiersFromSource(payload.Source);
        }
    }
}
