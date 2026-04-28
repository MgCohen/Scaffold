#nullable enable
using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class Variable : IEquatable<Variable>
    {
        public Variable(string key, string payloadTypeId = "string")
        {
            this.key = key ?? "";
            this.payloadTypeId = payloadTypeId ?? "string";
        }

        public Variable()
        {
        }

        public string Key => key ?? "";

        public string PayloadTypeId => payloadTypeId ?? "string";

        [SerializeField]
        private string key = "";

        [SerializeField]
        private string payloadTypeId = "string";

        public override bool Equals(object? obj)
        {
            return obj is Variable v && Equals(v);
        }

        public bool Equals(Variable? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Key == other.Key && PayloadTypeId == other.PayloadTypeId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, PayloadTypeId);
        }

        public static bool operator ==(Variable? a, Variable? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            return a.Equals(b);
        }

        public static bool operator !=(Variable? a, Variable? b)
        {
            return !(a == b);
        }
    }
}
