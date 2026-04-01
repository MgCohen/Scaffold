using System;
using System.Collections.Generic;

namespace Scaffold.Maps
{
    public readonly struct Index<TPrimary> : IEquatable<Index<TPrimary>>
    {
        public Index(TPrimary primary)
        {
            if (primary is null)
            {
                throw new ArgumentNullException(nameof(primary));
            }

            this.Primary = primary;
        }

        public readonly TPrimary Primary;

        public bool Equals(Index<TPrimary> other)
        {
            return EqualityComparer<TPrimary>.Default.Equals(Primary, other.Primary);
        }

        public override bool Equals(object obj)
        {
            if (obj is not Index<TPrimary> other)
            {
                return false;
            }

            return EqualityComparer<TPrimary>.Default.Equals(Primary, other.Primary);
        }

        public override int GetHashCode()
        {
            return Primary != null ? Primary.GetHashCode() : 0;
        }

        public static bool operator ==(Index<TPrimary> left, Index<TPrimary> right)
        {
            return EqualityComparer<TPrimary>.Default.Equals(left.Primary, right.Primary);
        }

        public static bool operator !=(Index<TPrimary> left, Index<TPrimary> right)
        {
            return EqualityComparer<TPrimary>.Default.Equals(left.Primary, right.Primary) == false;
        }
    }
}
