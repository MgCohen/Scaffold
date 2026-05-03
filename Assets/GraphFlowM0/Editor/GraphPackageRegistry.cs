using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.M0.Editor
{
    /// <summary>
    /// Per-package, per-runner lookup tables driven by generator-emitted registrations.
    /// The baker reads this instead of pattern-matching on editor node types, which is
    /// what makes the baker generic across packages (M1).
    /// </summary>
    public sealed class GraphPackageRegistry<TRunner> where TRunner : GraphRunner
    {
        // Returns RuntimeNode (base) so pure data nodes — extending RuntimeNode without TRunner —
        // can register through the same registry as flow-bearing RuntimeNode<TRunner> classes. The
        // executor only invokes Execute on RuntimeNode<TRunner> instances reached through flowEdges;
        // data nodes are never participants in flow.
        public delegate RuntimeNode NodeFactory(INode editorNode);

        public sealed class NodeRegistration
        {
            public Type EditorNodeType = null!;
            public NodeFactory Factory = null!;
            public Dictionary<string, int> DataInputPortIds = new(StringComparer.Ordinal);
            public Dictionary<string, int> DataOutputPortIds = new(StringComparer.Ordinal);
            public Dictionary<string, int> FlowInputPortIds = new(StringComparer.Ordinal);
            public Dictionary<string, int> FlowOutputPortIds = new(StringComparer.Ordinal);
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
