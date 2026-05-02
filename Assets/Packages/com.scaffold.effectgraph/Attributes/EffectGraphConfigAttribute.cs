using System;

namespace Scaffold.EffectGraph
{
    /// <summary>Port discovery strategy for generated command/entry nodes.</summary>
    public enum PortConvention
    {
        CommandResultPair,
        AttributedFields,
        MutableInReadOnlyOut,
        AllFieldsIn
    }

    /// <summary>
    /// Declares effect-graph generation for this assembly. Place exactly one on the consumer assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class EffectGraphConfigAttribute : Attribute
    {
        /// <summary>Metadata names (non-generic) or closed constructed types for game-command bases, e.g. <c>MyGame.GameCommand`1</c>.</summary>
        public string[] CommandBases { get; set; } = Array.Empty<string>();

        /// <summary>Metadata names for entry-point roots, e.g. <c>MyGame.EntryPoint</c>.</summary>
        public string[] EntryBases { get; set; } = Array.Empty<string>();

        public PortConvention Convention { get; set; }

        /// <summary>Root namespace for generated registry and node types.</summary>
        public string RegistryNamespace { get; set; } = "";
    }
}
