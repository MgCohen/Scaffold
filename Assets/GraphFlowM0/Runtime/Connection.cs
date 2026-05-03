using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.GraphFlow.M0
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
    /// Base runtime node — owns the dispatch dict and the connections list. Generated default ctors on
    /// concrete nodes populate <see cref="Ports"/> with each port handle keyed by port id; hydration
    /// appends to <see cref="Connections"/> through <see cref="Bind"/>.
    /// Pure data nodes (no flow, no runner) inherit from this directly.
    /// </summary>
    [Serializable]
    public abstract class RuntimeNode
    {
        public int nodeId;
        public string editorGuid = string.Empty;

        /// <summary>Port-id → port handle, populated by the generated ctor.</summary>
        [NonSerialized] public readonly Dictionary<int, Port> Ports = new();

        /// <summary>Wires built at hydration. Append-only; the executor never reads from here.</summary>
        [NonSerialized] public readonly List<Connection> Connections = new();

        /// <summary>
        /// Hydration entry-point. Looks up both ports through their nodes' dicts and routes through
        /// the single <see cref="Connection.Bind"/> seam. No per-node override needed.
        /// </summary>
        public Connection Bind(int dstPortId, RuntimeNode src, int srcPortId)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (!Ports.TryGetValue(dstPortId, out var dstPort))
                throw new ArgumentException($"Destination node has no port with id {dstPortId}.");
            if (!src.Ports.TryGetValue(srcPortId, out var srcPort))
                throw new ArgumentException($"Source node has no port with id {srcPortId}.");

            var conn = Connection.Bind(dstPort, srcPort);
            Connections.Add(conn);
            return conn;
        }
    }

    /// <summary>Flow-bearing runtime node — adds <see cref="Execute"/> over a typed runner.</summary>
    [Serializable]
    public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
    {
        public abstract Task<FlowContinuation> Execute(TRunner runner);
    }
}
