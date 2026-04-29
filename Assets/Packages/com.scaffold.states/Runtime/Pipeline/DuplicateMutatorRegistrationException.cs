#nullable enable

using System;

namespace Scaffold.States
{
    public sealed class DuplicateMutatorRegistrationException : Exception
    {
        public DuplicateMutatorRegistrationException(string message) : base(message)
        {
        }
    }
}
