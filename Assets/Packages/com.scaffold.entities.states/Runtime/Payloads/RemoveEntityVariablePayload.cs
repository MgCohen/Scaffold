using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    public sealed record RemoveEntityVariablePayload(InstanceId EntityId, Variable Variable);
}
