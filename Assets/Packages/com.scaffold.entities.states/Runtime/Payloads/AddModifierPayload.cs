using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    public sealed class AddModifierPayload
    {
        public AddModifierPayload(InstanceId entityId, Variable variable, VariableModifier modifier, ModifierId modifierId)
        {
            EntityId = entityId;
            Variable = variable;
            Modifier = modifier;
            ModifierId = modifierId;
        }

        public InstanceId EntityId { get; }

        public Variable Variable { get; }

        public VariableModifier Modifier { get; }

        public ModifierId ModifierId { get; }
    }
}
