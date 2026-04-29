using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    public sealed record RemoveModifierPayload(InstanceId EntityId, Variable Variable, ModifierId ModifierId);
}
