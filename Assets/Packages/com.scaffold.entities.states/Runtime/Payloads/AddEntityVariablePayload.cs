using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record AddEntityVariablePayload(Reference EntityRef, Variable Variable, VariableValue InitialValue) : IPayloadReference
    {
        public Reference GetReference() => EntityRef;
    }
}
