#nullable enable
using System;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// Immutable attribute payload for an entity instance. First-party identity uses <see cref="AttributeSO"/>
    /// references; <see cref="MatchKey"/> supports second-party string matching (see <see cref="EntityInstanceState"/>).
    /// </summary>
    [Serializable]
    public struct Attribute : IEquatable<Attribute>
    {
        public Attribute(string payload, string? matchKey = null)
        {
            this.payload = payload ?? string.Empty;
            this.matchKey = matchKey;
        }

        public string Payload => payload ?? string.Empty;

        [SerializeField]
        private string payload;

        public string? MatchKey => string.IsNullOrEmpty(matchKey) ? null : matchKey;

        [SerializeField]
        private string matchKey;

        public override bool Equals(object? obj)
        {
            return obj is Attribute other && Equals(other);
        }

        public bool Equals(Attribute other)
        {
            return Payload == other.Payload && MatchKey == other.MatchKey;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Payload != null ? Payload.GetHashCode() : 0) * 397) ^ (MatchKey != null ? MatchKey.GetHashCode() : 0);
            }
        }

        public static bool operator ==(Attribute left, Attribute right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Attribute left, Attribute right)
        {
            return !left.Equals(right);
        }
    }
}
