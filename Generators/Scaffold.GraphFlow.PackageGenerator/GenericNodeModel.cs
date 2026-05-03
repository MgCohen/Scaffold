using System.Collections.Immutable;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal readonly struct GenericNodeFlowPort
    {
        internal GenericNodeFlowPort(string name, int portId)
        {
            Name = name;
            PortId = portId;
        }

        internal string Name { get; }
        internal int PortId { get; }
    }

    internal readonly struct GenericNodeDataPort
    {
        internal GenericNodeDataPort(string fieldName, int portId, string csharpType)
        {
            FieldName = fieldName;
            PortId = portId;
            CSharpType = csharpType;
        }

        internal string FieldName { get; }
        internal int PortId { get; }
        /// <summary>The unwrapped data type (e.g. "bool" for an [Input] field of type Connection&lt;bool&gt;?).</summary>
        internal string CSharpType { get; }
    }

    /// <summary>
    /// Parsed shape of a hand-written <c>RuntimeNode&lt;TRunner&gt;</c> annotated with <c>[GraphNode]</c>.
    /// The generator emits one editor mirror + one registry entry per package whose runner closes <c>TRunner</c>.
    /// </summary>
    internal readonly struct GenericNodeModel
    {
        internal GenericNodeModel(
            string typeNamespace,
            string typeName,
            bool isGenericOverRunner,
            string? category,
            ImmutableArray<GenericNodeFlowPort> flowIns,
            ImmutableArray<GenericNodeFlowPort> flowOuts,
            ImmutableArray<GenericNodeDataPort> inputs,
            ImmutableArray<GenericNodeDataPort> outputs)
        {
            TypeNamespace = typeNamespace;
            TypeName = typeName;
            IsGenericOverRunner = isGenericOverRunner;
            Category = category;
            FlowIns = flowIns;
            FlowOuts = flowOuts;
            Inputs = inputs;
            Outputs = outputs;
        }

        internal string TypeNamespace { get; }
        internal string TypeName { get; }
        /// <summary>True when the runtime class is <c>Foo&lt;TRunner&gt;</c>; false when non-generic. The emitter closes the generic at the package's runner.</summary>
        internal bool IsGenericOverRunner { get; }
        internal string? Category { get; }
        internal ImmutableArray<GenericNodeFlowPort> FlowIns { get; }
        internal ImmutableArray<GenericNodeFlowPort> FlowOuts { get; }
        internal ImmutableArray<GenericNodeDataPort> Inputs { get; }
        internal ImmutableArray<GenericNodeDataPort> Outputs { get; }

        internal string FullyQualifiedNoGeneric =>
            string.IsNullOrEmpty(TypeNamespace) ? TypeName : TypeNamespace + "." + TypeName;
    }
}
