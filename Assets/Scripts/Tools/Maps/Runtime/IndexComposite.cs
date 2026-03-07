using System;

namespace Scaffold.Maps
{
    public readonly struct Index<TPrimary, TSecondary> : IEquatable<Index<TPrimary, TSecondary>>
    {
        public readonly TPrimary primary;
        public readonly TSecondary secondary;

        public Index(TPrimary primary, TSecondary secondary)
        {
            this.primary = primary;
            this.secondary = secondary;
        }

        public bool Equals(Index<TPrimary, TSecondary> other)
        {
            return Equals(primary, other.primary) && Equals(secondary, other.secondary);
        }

        public override bool Equals(object obj)
        {
            return obj is Index<TPrimary, TSecondary> other && Equals(other);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (primary != null ? primary.GetHashCode() : 0);
            hash = hash * 31 + (secondary != null ? secondary.GetHashCode() : 0);
            return hash;
        }

        public static bool operator ==(Index<TPrimary, TSecondary> left, Index<TPrimary, TSecondary> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Index<TPrimary, TSecondary> left, Index<TPrimary, TSecondary> right)
        {
            return left.Equals(right) == false;
        }
    }
}
