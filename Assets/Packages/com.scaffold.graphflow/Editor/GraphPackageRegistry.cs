using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    /// <summary>
    /// Per-package, per-runner lookup tables driven by generator-emitted registrations.
    /// The baker reads this instead of pattern-matching on editor node types, which is
    /// what makes the baker generic across packages (M1).
    ///
    /// <para>Post-M3 phase 2 (decision #4): port IDs are strings (field names). The dicts here are
    /// just <see cref="HashSet{T}"/>s of valid port names per role — no name → id translation needed
    /// because the name IS the id end-to-end (asset, runtime, editor).</para>
    /// </summary>
    public sealed class GraphPackageRegistry<TRunner> where TRunner : GraphRunner
    {
        // Returns RuntimeNode (base) so pure data nodes — extending RuntimeNode without TRunner —
        // can register through the same registry as flow-bearing RuntimeNode<TRunner> classes.
        public delegate RuntimeNode NodeFactory(INode editorNode);

        public sealed class NodeRegistration
        {
            public Type EditorNodeType = null!;
            public NodeFactory Factory = null!;
            public HashSet<string> DataInputPortNames = new(StringComparer.Ordinal);
            public HashSet<string> DataOutputPortNames = new(StringComparer.Ordinal);
            public HashSet<string> FlowInputPortNames = new(StringComparer.Ordinal);
            public HashSet<string> FlowOutputPortNames = new(StringComparer.Ordinal);
            // Non-null only for entry-shaped nodes; carries the AssemblyQualifiedName of the payload
            // so the baker can populate EntryIndex.entryTypeId without reflection on the runtime side.
            public string? EntryTypeId;
        }

        readonly Dictionary<Type, NodeRegistration> _byEditorType = new();

        public void Register(NodeRegistration r) => _byEditorType[r.EditorNodeType] = r;

        public NodeRegistration? Lookup(Type editorType) =>
            _byEditorType.TryGetValue(editorType, out var v) ? v : null;
    }
}
