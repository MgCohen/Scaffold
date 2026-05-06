#nullable enable
using System;

namespace Scaffold.States
{
    public sealed record Ref<T>(Guid Id) : Reference
    {
        public override string ToString() => $"Ref<{typeof(T).Name}>({Id:N})";
    }
}
