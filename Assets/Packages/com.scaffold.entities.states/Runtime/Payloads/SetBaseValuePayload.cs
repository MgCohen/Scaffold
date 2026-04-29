using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    public sealed record SetBaseValuePayload(InstanceId EntityId, Variable Variable, VariableValue Value);
}
