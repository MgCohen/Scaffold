#nullable enable
using Scaffold.States;

namespace Scaffold.Entities.States
{
    [Mutator]
    internal sealed class ClearModifiersMutator : Mutator<EntityState, ClearModifiersPayload>
    {
        public override EntityState Change(EntityState state, ClearModifiersPayload payload, IStateScope scope)
        {
            return state.WithoutAllModifiers();
        }
    }
}
