using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record RemoveModifierPayload(InstanceId EntityId, Variable Variable, ModifierId ModifierId) : IPayloadReference
    {
        public IReference GetReference()
        {
            return EntityStateReference.From(EntityId);
        }
    }
}
