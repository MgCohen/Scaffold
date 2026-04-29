#nullable enable

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class AddModifierMutator : Mutator<EntityVariableState, AddModifierPayload>
    {
        public override EntityVariableState Change(EntityVariableState state, AddModifierPayload payload, IStateScope scope)
        {
            return state.WithModifier(payload.Variable, new ActiveModifier(payload.ModifierId, payload.Modifier, payload.Source));
        }
    }
}
