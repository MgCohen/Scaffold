#nullable enable

using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class RemoveModifiersBySourceMutator : Mutator<EntityVariableState, RemoveModifiersBySourcePayload>
    {
        public override EntityVariableState Change(EntityVariableState state, RemoveModifiersBySourcePayload payload, IStateScope scope)
        {
            return state.WithoutModifiersFromSource(payload.Source);
        }
    }
}
