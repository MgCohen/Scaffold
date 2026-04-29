using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    public sealed class AddEntityVariablePayload
    {
        public AddEntityVariablePayload(InstanceId entityId, Variable variable, VariableValue initialValue)
        {
            EntityId = entityId;
            Variable = variable;
            InitialValue = initialValue;
        }

        public InstanceId EntityId { get; }

        public Variable Variable { get; }

        public VariableValue InitialValue { get; }
    }
}
