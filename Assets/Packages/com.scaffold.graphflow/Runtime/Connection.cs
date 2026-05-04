using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

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
    /// Base runtime node — owns the dispatch dict and the connections list. Generated default ctors on
    /// concrete nodes populate <see cref="Ports"/> with each port handle keyed by port name; hydration
    /// appends to <see cref="Connections"/> through <see cref="Bind"/>.
    ///
    /// <para>Post-M3 phase 2 (decision #5): every node — data and flow — derives from this base. The
    /// <see cref="Execute"/> default returns <see cref="Task.CompletedTask"/> (= stop). Flow-bearing
    /// nodes that need typed runner access derive from <see cref="RuntimeNode{TRunner}"/> instead;
    /// runner-agnostic flow nodes (Branch, Cancel, Return, Not) override <see cref="Execute"/>
    /// directly here.</para>
    /// </summary>
    [Serializable]
    public abstract class RuntimeNode
    {
        public int nodeId;
        public string editorGuid = string.Empty;

        /// <summary>Port-name → port handle, populated by the generated ctor.</summary>
        [NonSerialized] public readonly Dictionary<string, Port> Ports = new();

        /// <summary>Wires built at hydration. Append-only; the executor never reads from here.</summary>
        [NonSerialized] public readonly List<Connection> Connections = new();

        /// <summary>
        /// Default no-op = stop-the-walk. Override in flow-bearing nodes. The two-tier hierarchy
        /// (decision #5) makes this the single dispatch point — <see cref="RuntimeNode{TRunner}"/>
        /// seals an override that delegates to its typed body.
        /// </summary>
        public virtual Task Execute(Flow flow) => Task.CompletedTask;

        /// <summary>
        /// Hydration entry-point. Looks up both ports through their nodes' dicts and routes through
        /// the single <see cref="Connection.Bind"/> seam. No per-node override needed.
        /// </summary>
        public Connection Bind(string dstPortName, RuntimeNode src, string srcPortName)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (!Ports.TryGetValue(dstPortName, out var dstPort))
                throw new ArgumentException($"Destination node has no port named '{dstPortName}'.");
            if (!src.Ports.TryGetValue(srcPortName, out var srcPort))
                throw new ArgumentException($"Source node has no port named '{srcPortName}'.");

            var conn = Connection.Bind(dstPort, srcPort);
            Connections.Add(conn);
            return conn;
        }
    }

    /// <summary>
    /// Flow-bearing runtime node with typed runner access. Custom dispatchers that need to reach the
    /// runner's services derive from here; built-in primitives (Branch/Cancel/Return/Not) drop TRunner
    /// and live on <see cref="RuntimeNode"/> directly.
    ///
    /// <para>The runner reference is bound once at hydration via <see cref="BindRunner"/> and read via
    /// the <c>_runner</c> backing field — per-Execute dispatch is a direct field access, no cast.</para>
    /// </summary>
    [Serializable]
    public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
    {
#nullable enable
        [NonSerialized] TRunner? _runner;
#nullable disable

        /// <summary>Called by <c>GraphController.Initialize</c> so per-Execute dispatch is reflection-free.</summary>
        internal void BindRunner(TRunner runner) => _runner = runner;

        public sealed override Task Execute(Flow flow) => Execute(_runner, flow);
        public abstract Task Execute(TRunner runner, Flow flow);
    }
}
