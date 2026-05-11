using Scaffold.Entities;
using Scaffold.States;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.States
{
    public sealed record RemoveModifierPayload(Reference EntityRef, Variable Variable, ModifierId ModifierId) : IPayloadReference
    {
        public Reference GetReference() => EntityRef;
    }
}
