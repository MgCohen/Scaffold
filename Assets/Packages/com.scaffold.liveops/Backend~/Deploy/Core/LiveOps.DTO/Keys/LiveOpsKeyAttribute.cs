using System;

namespace LiveOps.DTO.Keys
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class LiveOpsKeyAttribute : Attribute
    {
        public string Value { get; }

        public LiveOpsKeyAttribute(string value) => Value = value;
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GameApiRequestAttribute : Attribute
    {
        public string WireKey { get; }

        public GameApiRequestAttribute(string wireKey = null) => WireKey = wireKey;
    }
}
