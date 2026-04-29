#nullable enable

using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class RemoveModifierMutator : Mutator<EntityVariableState, RemoveModifierPayload>
    {
        public override EntityVariableState Change(EntityVariableState state, RemoveModifierPayload payload, IStateScope scope)
        {
            return state.WithoutModifier(payload.Variable, payload.ModifierId);
        }
    }
}
