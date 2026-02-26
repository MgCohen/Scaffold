using System;

namespace Scaffold.Maps
{
    public readonly struct Index<TPrimary> : IEquatable<Index<TPrimary>>
    {
        public readonly TPrimary primary;

        public Index(TPrimary primary)
        {
            this.primary = primary;
        }

        public bool Equals(Index<TPrimary> other)
        {
            return Equals(primary, other.primary);
        }

        public override bool Equals(object obj)
        {
            return obj is Index<TPrimary> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return primary != null ? primary.GetHashCode() : 0;
        }

        public static bool operator ==(Index<TPrimary> left, Index<TPrimary> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Index<TPrimary> left, Index<TPrimary> right)
        {
            return left.Equals(right) == false;
        }
    }
}
