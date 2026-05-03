#nullable enable

using System;

namespace Scaffold.States
{
    public sealed class MutatorNotRegisteredException : Exception
    {
        public MutatorNotRegisteredException(Type payloadType)
            : base($"No mutators registered for payload type {payloadType.FullName}.")
        {
            PayloadType = payloadType;
        }

        public Type PayloadType { get; }
    }
}
