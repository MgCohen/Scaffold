#nullable enable
using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class Variable : IEquatable<Variable>
    {
        public Variable(string key, VariableValueType type = VariableValueType.String)
        {
            this.key = key ?? "";
            this.type = type;
        }

        public Variable()
        {
        }

        public string Key => key ?? "";

        [SerializeField]
        private string key = "";

        public VariableValueType Type => type;

        [SerializeField]
        private VariableValueType type = VariableValueType.String;

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

            return Key == other.Key && Type == other.Type;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, Type);
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
