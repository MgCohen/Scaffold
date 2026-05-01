using System;

namespace Scaffold.GraphFlow
{
    public readonly struct NodeId : IEquatable<NodeId>
    {
        public Guid Value { get; }

        public NodeId(Guid value) => Value = value;

        public static NodeId New() => new(Guid.NewGuid());

        public bool Equals(NodeId other) => Value.Equals(other.Value);

        public override bool Equals(object obj) => obj is NodeId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(NodeId left, NodeId right) => left.Equals(right);

        public static bool operator !=(NodeId left, NodeId right) => !left.Equals(right);
    }
}
