using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Scaffold.EffectGraph.Sample.Editor")]

namespace Scaffold.EffectGraph.Editor
{
    /// <summary>Registry hooks for generated nodes (expanded in milestone 2).</summary>
    public static class EffectGraphRegistry
    {
        static readonly List<(string TypeId, System.Type EditorNodeType)> s_entries = new();

        public static void Clear() => s_entries.Clear();

        public static void Register(string typeId, System.Type editorNodeType) =>
            s_entries.Add((typeId, editorNodeType));

        public static IReadOnlyList<(string TypeId, System.Type EditorNodeType)> Entries => s_entries;
    }
}
