#nullable enable
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record ClearModifiersPayload(Reference EntityRef) : IPayloadReference
    {
        public Reference GetReference() => EntityRef;
    }
}
