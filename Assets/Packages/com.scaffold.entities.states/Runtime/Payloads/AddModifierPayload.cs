using Scaffold.Entities;
using Scaffold.States;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.States
{
    public sealed record AddModifierPayload(Reference EntityRef, Variable Variable, VariableModifier Modifier, ModifierId ModifierId, ModifierSource? Source = null) : IPayloadReference
    {
        public Reference GetReference() => EntityRef;
    }
}
