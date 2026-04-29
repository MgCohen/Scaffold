using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    public sealed record AddEntityVariablePayload(
        InstanceId EntityId,
        Variable Variable,
        VariableValue InitialValue);
}
