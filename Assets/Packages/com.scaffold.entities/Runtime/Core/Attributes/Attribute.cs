#nullable enable
using System;

namespace Scaffold.Entities
{
    public sealed class Attribute : IEquatable<Attribute>
    {
        public Attribute(
            string key,
            AttributeValueType type = AttributeValueType.String,
            string? customValueTypeName = null,
            string? valueKindId = null)
        {
            Key = key ?? string.Empty;
            Type = type;
            CustomValueTypeName = NormalizeCustomName(type, customValueTypeName);
            ValueKindId = string.IsNullOrWhiteSpace(valueKindId) ? null : valueKindId.Trim();
        }

        public string Key { get; }

        public AttributeValueType Type { get; }

        public string? CustomValueTypeName { get; }

        public string? ValueKindId { get; }

        public bool Equals(Attribute? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Key, other.Key, StringComparison.Ordinal)
                   && Type == other.Type
                   && string.Equals(CustomValueTypeName, other.CustomValueTypeName, StringComparison.Ordinal)
                   && string.Equals(ValueKindId, other.ValueKindId, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => Equals(obj as Attribute);

        public override int GetHashCode() => HashCode.Combine(Key, Type, CustomValueTypeName, ValueKindId);

        public static bool operator ==(Attribute? left, Attribute? right) => Equals(left, right);

        public static bool operator !=(Attribute? left, Attribute? right) => !Equals(left, right);

        private static string? NormalizeCustomName(AttributeValueType type, string? customValueTypeName)
        {
            if (type != AttributeValueType.Custom)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(customValueTypeName) ? null : customValueTypeName.Trim();
        }
    }
}
