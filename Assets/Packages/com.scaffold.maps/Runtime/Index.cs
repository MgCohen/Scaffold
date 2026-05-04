using System;
using System.Collections.Generic;

namespace Scaffold.Maps
{
    public readonly struct Index<TPrimary, TSecondary> : IEquatable<Index<TPrimary, TSecondary>>
    {
        public Index(TPrimary primary, TSecondary secondary)
        {
            // Avoid `primary is null` on unconstrained T when T is a value type: some runtimes emit
            // boxing or extra allocs on the generic null check; gate on type kind first.
            if (!typeof(TPrimary).IsValueType && primary is null)
            {
                throw new ArgumentNullException(nameof(primary));
            }

            if (!typeof(TSecondary).IsValueType && secondary is null)
            {
                throw new ArgumentNullException(nameof(secondary));
            }

            this.Primary = primary;
            this.Secondary = secondary;
        }

        public readonly TPrimary Primary;
        public readonly TSecondary Secondary;

        public bool Equals(Index<TPrimary, TSecondary> other)
        {
            bool primaryEquals = EqualityComparer<TPrimary>.Default.Equals(Primary, other.Primary);
            bool secondaryEquals = EqualityComparer<TSecondary>.Default.Equals(Secondary, other.Secondary);
            return primaryEquals && secondaryEquals;
        }

        public override bool Equals(object obj)
        {
            if (obj is not Index<TPrimary, TSecondary> other)
            {
                return false;
            }

            bool primaryEquals = EqualityComparer<TPrimary>.Default.Equals(Primary, other.Primary);
            bool secondaryEquals = EqualityComparer<TSecondary>.Default.Equals(Secondary, other.Secondary);
            return primaryEquals && secondaryEquals;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (Primary != null ? Primary.GetHashCode() : 0);
            hash = hash * 31 + (Secondary != null ? Secondary.GetHashCode() : 0);
            return hash;
        }

        public static bool operator ==(Index<TPrimary, TSecondary> left, Index<TPrimary, TSecondary> right)
        {
            bool primaryEquals = EqualityComparer<TPrimary>.Default.Equals(left.Primary, right.Primary);
            bool secondaryEquals = EqualityComparer<TSecondary>.Default.Equals(left.Secondary, right.Secondary);
            return primaryEquals && secondaryEquals;
        }

        public static bool operator !=(Index<TPrimary, TSecondary> left, Index<TPrimary, TSecondary> right)
        {
            bool primaryEquals = EqualityComparer<TPrimary>.Default.Equals(left.Primary, right.Primary);
            bool secondaryEquals = EqualityComparer<TSecondary>.Default.Equals(left.Secondary, right.Secondary);
            return (primaryEquals && secondaryEquals) == false;
        }
    }
}
