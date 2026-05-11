using Scaffold.Entities;
using Scaffold.States;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.States
{
    public sealed record SetBaseValuePayload(Reference EntityRef, Variable Variable, VariableValue Value) : IPayloadReference
    {
        public Reference GetReference() => EntityRef;
    }
}
