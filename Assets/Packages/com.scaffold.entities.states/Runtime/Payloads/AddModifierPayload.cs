using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    public sealed record AddModifierPayload(
        InstanceId EntityId,
        Variable Variable,
        VariableModifier Modifier,
        ModifierId ModifierId);
}
