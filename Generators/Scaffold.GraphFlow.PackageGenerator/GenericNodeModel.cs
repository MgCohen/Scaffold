using System.Collections.Immutable;

namespace Scaffold.GraphFlow.PackageGenerator
{
    /// <summary>One typed input field on a <c>[GraphNode]</c> class.</summary>
    internal readonly struct GenericNodeInputField
    {
        internal GenericNodeInputField(string fieldName, int portId, string csharpType)
        {
            FieldName = fieldName;
            PortId = portId;
            CSharpType = csharpType;
        }

        internal string FieldName { get; }
        internal int PortId { get; }
        /// <summary>Display string for the type argument T in <c>InputPort&lt;T&gt;</c>.</summary>
        internal string CSharpType { get; }
    }

    /// <summary>One typed output field on a <c>[GraphNode]</c> class.</summary>
    internal readonly struct GenericNodeOutputField
    {
        internal GenericNodeOutputField(string fieldName, int portId, string csharpType)
        {
            FieldName = fieldName;
            PortId = portId;
            CSharpType = csharpType;
        }

        internal string FieldName { get; }
        internal int PortId { get; }
        internal string CSharpType { get; }
    }

    /// <summary>One flow-out field on a <c>[GraphNode]</c> class.</summary>
    internal readonly struct GenericNodeFlowOut
    {
        internal GenericNodeFlowOut(string fieldName, int portId)
        {
            FieldName = fieldName;
            PortId = portId;
        }

        internal string FieldName { get; }
        internal int PortId { get; }
    }

    /// <summary>
    /// Parsed shape of a hand-written <c>RuntimeNode</c> (data) or <c>RuntimeNode&lt;TRunner&gt;</c>
    /// (flow) annotated with <c>[GraphNode]</c>. The generator emits a partial completing the runtime
    /// (default ctor that constructs port handles + populates the <c>Ports</c> dict) plus the editor
    /// mirror + registry entry per package whose runner closes <c>TRunner</c>.
    /// </summary>
    internal readonly struct GenericNodeModel
    {
        internal GenericNodeModel(
            string typeNamespace,
            string typeName,
            bool isFlowNode,
            bool isGenericOverRunner,
            string? category,
            int implicitFlowInPortId,
            ImmutableArray<GenericNodeFlowOut> flowOuts,
            ImmutableArray<GenericNodeInputField> inputs,
            ImmutableArray<GenericNodeOutputField> outputs,
            bool hasInitializePortsHook)
        {
            TypeNamespace = typeNamespace;
            TypeName = typeName;
            IsFlowNode = isFlowNode;
            IsGenericOverRunner = isGenericOverRunner;
            Category = category;
            ImplicitFlowInPortId = implicitFlowInPortId;
            FlowOuts = flowOuts;
            Inputs = inputs;
            Outputs = outputs;
            HasInitializePortsHook = hasInitializePortsHook;
        }

        internal string TypeNamespace { get; }
        internal string TypeName { get; }
        /// <summary>True when the class derives from <c>RuntimeNode&lt;TRunner&gt;</c>; false for pure data nodes deriving from <c>RuntimeNode</c>.</summary>
        internal bool IsFlowNode { get; }
        /// <summary>True for <c>Foo&lt;TRunner&gt;</c>; false for non-generic <c>Foo</c>.</summary>
        internal bool IsGenericOverRunner { get; }
        internal string? Category { get; }
        /// <summary>Port id 0 for the implicit flow-in (only meaningful when <see cref="IsFlowNode"/>).</summary>
        internal int ImplicitFlowInPortId { get; }
        internal ImmutableArray<GenericNodeFlowOut> FlowOuts { get; }
        internal ImmutableArray<GenericNodeInputField> Inputs { get; }
        internal ImmutableArray<GenericNodeOutputField> Outputs { get; }
        /// <summary>True if the class declares <c>partial void InitializePorts()</c> with a body — the generated ctor calls it after constructing inputs.</summary>
        internal bool HasInitializePortsHook { get; }

        internal string FullyQualifiedNoGeneric =>
            string.IsNullOrEmpty(TypeNamespace) ? TypeName : TypeNamespace + "." + TypeName;
    }
}
