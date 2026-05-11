using Scaffold.Entities;
using Scaffold.States;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.States
{
    public sealed record RemoveEntityVariablePayload(Reference EntityRef, Variable Variable) : IPayloadReference
    {
        public Reference GetReference() => EntityRef;
    }
}
