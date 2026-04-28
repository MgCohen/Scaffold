using System;

namespace Scaffold.Entities
{
    public readonly struct ModifierId : IEquatable<ModifierId>
    {
        public ModifierId(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; }

        public override bool Equals(object obj)
        {
            return obj is ModifierId other && Equals(other);
        }

        public bool Equals(ModifierId other)
        {
            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static ModifierId New()
        {
            return new ModifierId(Guid.NewGuid());
        }

        public static bool operator ==(ModifierId left, ModifierId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModifierId left, ModifierId right)
        {
            return !left.Equals(right);
        }
    }
}
