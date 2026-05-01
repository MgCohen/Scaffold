#nullable enable

using System;

namespace Scaffold.Entities
{
    public readonly struct ModifierSource : IEquatable<ModifierSource>
    {
        public ModifierSource(InstanceId source, int tag = 0)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Tag = tag;
        }

        public InstanceId Source { get; }
        public int Tag { get; }

        public override bool Equals(object? obj)
        {
            return obj is ModifierSource other && Equals(other);
        }

        public bool Equals(ModifierSource other)
        {
            return Source.Equals(other.Source) && Tag == other.Tag;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Source, Tag);
        }

        public static bool operator ==(ModifierSource left, ModifierSource right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModifierSource left, ModifierSource right)
        {
            return !left.Equals(right);
        }
    }
}
