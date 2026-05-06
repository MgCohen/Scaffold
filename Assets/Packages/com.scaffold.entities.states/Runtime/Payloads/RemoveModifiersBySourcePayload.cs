using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record RemoveModifiersBySourcePayload(Reference EntityRef, ModifierSource Source) : IPayloadReference
    {
        public Reference GetReference() => EntityRef;
    }
}
