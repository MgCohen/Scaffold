using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Marks a hand-written <c>RuntimeNode&lt;TRunner&gt;</c> for generic-node emission.
    /// The author writes the runtime class; the generator emits the matching editor node + registry entry per package whose runner the class is parameterized over.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class GraphNodeAttribute : Attribute
    {
        public string? Category { get; set; }
    }

    /// <summary>
    /// Declares a flow-input port on a <c>[GraphNode]</c> class. Marker-only — flow ports carry no data, so no field is needed.
    /// Repeat the attribute to declare multiple flow-input ports.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class FlowInAttribute : Attribute
    {
        public FlowInAttribute(int portId, string name = "FlowIn")
        {
            PortId = portId;
            Name = name;
        }

        public int PortId { get; }
        public string Name { get; }
    }

    /// <summary>
    /// Declares a flow-output port on a <c>[GraphNode]</c> class. The port id is what <c>FlowContinuation.Next(...)</c> returns from <c>Execute</c>.
    /// Repeat the attribute to declare multiple flow-output ports (e.g. Branch True/False).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class FlowOutAttribute : Attribute
    {
        public FlowOutAttribute(int portId, string name)
        {
            PortId = portId;
            Name = name;
        }

        public int PortId { get; }
        public string Name { get; }
    }

    /// <summary>
    /// Marks a <c>Connection&lt;T&gt;?</c> field on a <c>[GraphNode]</c> class as a typed editor input port with the given stable port id.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class InputAttribute : Attribute
    {
        public InputAttribute(int portId)
        {
            PortId = portId;
        }

        public int PortId { get; }
    }

    /// <summary>
    /// Marks a typed field on a <c>[GraphNode]</c> class as a typed editor output port. The field's runtime value is read through <c>GetOutputConnection</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class OutputAttribute : Attribute
    {
        public OutputAttribute(int portId)
        {
            PortId = portId;
        }

        public int PortId { get; }
    }
}
