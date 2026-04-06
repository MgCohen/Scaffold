using System;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// Stable unique id for an entity instance (serialization-friendly wrapper).
    /// </summary>
    [Serializable]
    public struct InstanceId : IEquatable<InstanceId>
    {
        private InstanceId(string value)
        {
            this.value = value;
        }

        public bool IsEmpty => string.IsNullOrEmpty(value);

        [SerializeField]
        private string value;

        public Guid ToGuid()
        {
            return Guid.TryParseExact(value, "N", out Guid g) ? g : Guid.TryParse(value, out g) ? g : Guid.Empty;
        }

        public override bool Equals(object? obj)
        {
            return obj is InstanceId other && Equals(other);
        }

        public bool Equals(InstanceId other)
        {
            return value == other.value;
        }

        public override int GetHashCode()
        {
            return value != null ? value.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return value ?? string.Empty;
        }

        public static InstanceId New()
        {
            return new InstanceId(Guid.NewGuid().ToString("N"));
        }

        public static InstanceId FromGuid(Guid guid)
        {
            return new InstanceId(guid.ToString("N"));
        }

        public static bool operator ==(InstanceId left, InstanceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InstanceId left, InstanceId right)
        {
            return !left.Equals(right);
        }
    }
}
