#nullable enable
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Scaffold.Variables
{
    [Serializable]
    public sealed class Variable : IEquatable<Variable>
    {
        [SerializeField, FormerlySerializedAs("key")] private string id = "";
        [SerializeField, FormerlySerializedAs("payloadTypeId")] private string typeName = "";

        public string Id => id ?? "";
        public string TypeName => typeName ?? "";

        public Variable() { }

        public Variable(string id, string typeName)
        {
            this.id = id ?? "";
            this.typeName = typeName ?? "";
        }

        public bool Equals(Variable? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        public override bool Equals(object? obj) => obj is Variable v && Equals(v);

        public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);

        public static bool operator ==(Variable? a, Variable? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.Equals(b);
        }

        public static bool operator !=(Variable? a, Variable? b) => !(a == b);
    }
}
