using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record RemoveEntityVariablePayload(Reference EntityRef, Variable Variable) : IPayloadReference
    {
        public Reference GetReference() => EntityRef;
    }
}
