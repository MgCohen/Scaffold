using System;

namespace Scaffold.GraphFlow
{
    public enum PortDirection
    {
        Input,
        Output,
    }

    /// <summary>
    /// Compile-time port descriptor — Name + Type + Direction. Replaces the previous
    /// EventPortMeta + the implicit input/output split: one shape covers every port the catalog
    /// records, regardless of whether the host concept is an event field, command input,
    /// command output, or entry payload field.
    /// <para>Carried inside <see cref="CatalogEntry.Ports"/> so editor mirrors and bake factories
    /// iterate the same data with a single direction-switching loop.</para>
    /// </summary>
    [Serializable]
    public readonly struct PortMeta
    {
        public string        Name      { get; }
        public Type          Type      { get; }
        public PortDirection Direction { get; }

        public PortMeta(string name, Type type, PortDirection direction)
        {
            Name      = name;
            Type      = type;
            Direction = direction;
        }
    }
}
