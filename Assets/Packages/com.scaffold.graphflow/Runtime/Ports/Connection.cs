using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Runtime wire — built at hydration time, never serialized. Holds a typed reference from an output
    /// port on the source node to an input port on the destination node.
    ///
    /// <para><b>The single cast/conversion seam.</b> <see cref="Bind(Port, Port)"/> is the one place in
    /// the runtime where untyped ports become typed wires. Future type-conversion (`int → string` auto-
    /// coercion via a registered converter, etc.) plugs in here. M2 keeps it strict — type mismatch
    /// throws.</para>
    /// </summary>
    public abstract class Connection
    {
        /// <summary>
        /// Bind an input port to an output port. Dispatches through the input's type-revealing
        /// <see cref="Port.AcceptOutput"/> virtual so the typed cast happens once, statically, on the
        /// input side. Returns the constructed <see cref="Connection{T}"/> for the caller to record on
        /// the destination node's <see cref="RuntimeNode.Connections"/> list.
        /// </summary>
        public static Connection Bind(Port input, Port output)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));
            return input.AcceptOutput(output);
        }
    }

    public sealed class Connection<T> : Connection
    {
        public InputPort<T> Input { get; }
        public OutputPort<T> Output { get; }

        internal Connection(InputPort<T> input, OutputPort<T> output)
        {
            Input = input;
            Output = output;
        }

        public T Read() => Output.Read();
    }

    /// <summary>
    /// Routing wire — built at hydration time, never serialized. Pairs a <see cref="FlowOutPort"/>
    /// (source) with a <see cref="FlowInPort"/> (destination); both endpoints carry a back-ref
    /// to the same instance so the executor can walk forward via the source and validation can
    /// ask the destination port whether it's connected. Symmetric with <see cref="Connection{T}"/>
    /// for data — same hydration-once / direct-refs-thereafter pattern.
    /// </summary>
    public sealed class FlowConnection : Connection
    {
        public FlowOutPort Source { get; }
        public FlowInPort Destination { get; }

        internal FlowConnection(FlowOutPort source, FlowInPort destination)
        {
            Source = source;
            Destination = destination;
        }
    }
}
