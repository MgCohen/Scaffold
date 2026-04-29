using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    public sealed class RemoveModifierPayload
    {
        public RemoveModifierPayload(InstanceId entityId, Variable variable, ModifierId modifierId)
        {
            EntityId = entityId;
            Variable = variable;
            ModifierId = modifierId;
        }

        public InstanceId EntityId { get; }

        public Variable Variable { get; }

        public ModifierId ModifierId { get; }
    }
}
