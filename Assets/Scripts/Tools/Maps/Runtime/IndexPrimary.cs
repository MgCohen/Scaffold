using System;

namespace Scaffold.Maps
{
    public readonly struct Index<TPrimary> : IEquatable<Index<TPrimary>>
    {
        public readonly TPrimary Primary;

        public Index(TPrimary primary)
        {
            this.Primary = primary;
        }

        public bool Equals(Index<TPrimary> other)
        {
            return Equals(Primary, other.Primary);
        }

        public override bool Equals(object obj)
        {
            return obj is Index<TPrimary> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Primary != null ? Primary.GetHashCode() : 0;
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
