using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    public sealed class SetBaseValuePayload
    {
        public SetBaseValuePayload(InstanceId entityId, Variable variable, VariableValue value)
        {
            EntityId = entityId;
            Variable = variable;
            Value = value;
        }

        public InstanceId EntityId { get; }

        public Variable Variable { get; }

        public VariableValue Value { get; }
    }
}
