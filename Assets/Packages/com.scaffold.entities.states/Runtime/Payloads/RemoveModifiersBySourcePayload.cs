using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record RemoveModifiersBySourcePayload(InstanceId EntityId, ModifierSource Source) : IPayloadReference
    {
        public IReference GetReference()
        {
            return EntityId;
        }
    }
}
