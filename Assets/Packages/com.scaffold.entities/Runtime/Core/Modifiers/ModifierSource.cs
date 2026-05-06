#nullable enable
using System;
using Scaffold.States;

namespace Scaffold.Entities
{
    public readonly struct ModifierSource : IEquatable<ModifierSource>
    {
        public ModifierSource(Reference source, int tag = 0)
        {
            Source = source;
            Tag = tag;
        }

        public Reference? Source { get; }
        public int Tag { get; }

        public override bool Equals(object? obj) => obj is ModifierSource other && Equals(other);

        public bool Equals(ModifierSource other)
        {
            if (Source is null) return other.Source is null && Tag == other.Tag;
            return Source.Equals(other.Source) && Tag == other.Tag;
        }

        public override int GetHashCode() => HashCode.Combine(Source, Tag);

        public static bool operator ==(ModifierSource left, ModifierSource right) => left.Equals(right);
        public static bool operator !=(ModifierSource left, ModifierSource right) => !left.Equals(right);
    }
}
