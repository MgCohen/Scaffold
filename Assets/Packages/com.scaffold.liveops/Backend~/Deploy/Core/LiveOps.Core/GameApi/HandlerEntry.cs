using System;

namespace LiveOps.GameApi
{
    public sealed class HandlerEntry
    {
        public Type RequestType { get; init; } = null!;

        public Type ResponseType { get; init; } = null!;

        public Type HandlerType { get; init; } = null!;
    }
}
