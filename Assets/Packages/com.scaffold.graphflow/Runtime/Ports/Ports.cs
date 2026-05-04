using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Base for typed port handles on a <see cref="RuntimeNode"/>. Indexed by port id in the node's
    /// <see cref="RuntimeNode.Ports"/> dictionary. Carries no public logic — input/output split + read
    /// behaviour live on the typed subclasses; the type-revealing <see cref="AcceptOutput"/> hook is
    /// the one internal virtual that lets <see cref="Connection.Bind"/> resolve untyped ports to a
    /// typed wire without reflection.
    /// </summary>
    public abstract class Port
    {
        /// <summary>
        /// Internal type-revealing dispatch for <see cref="Connection.Bind"/>. Default rejects —
        /// only <see cref="InputPort{T}"/> overrides to do the typed match. <see cref="OutputPort{T}"/>
        /// is never the receiver of binding; the wire flows from output to input.
        /// </summary>
        internal virtual Connection AcceptOutput(Port output) =>
            throw new InvalidOperationException($"Bind direction must be input.AcceptOutput(output); got {GetType()} on the input side.");
    }

    /// <summary>
    /// Typed data input. Wired at hydration via <see cref="Connection.Bind"/> (which routes through
    /// <see cref="AcceptOutput"/> below). <see cref="Read"/> returns the upstream value when wired,
    /// or the lazy fallback (or <c>default</c>) when unwired — payload dispatcher runtimes pass a
    /// fallback closure that reads the inline-default field set by Unity deserialization.
    /// </summary>
    public sealed class InputPort<T> : Port
    {
        readonly Func<T>? _fallback;
        Connection<T>? _conn;

        public InputPort() { }
        public InputPort(Func<T> fallback) => _fallback = fallback;

        public T Read() => _conn != null ? _conn.Read() : (_fallback != null ? _fallback() : default!);

        /// <summary>
        /// The single cast in the system. <see cref="Connection.Bind"/> calls this; the typed
        /// <typeparamref name="T"/> is statically known here, so the only runtime check is whether
        /// <paramref name="output"/> matches. M2 throws on mismatch; M4 grows a converter lookup
        /// before the throw.
        /// </summary>
        internal override Connection AcceptOutput(Port output)
        {
            if (output is not OutputPort<T> typedOut)
                throw new InvalidOperationException($"Bind: output port type {output.GetType()} does not match input port type InputPort<{typeof(T)}>. (Type-conversion converters are M4 polish.)");

            var conn = new Connection<T>(this, typedOut);
            _conn = conn;
            return conn;
        }
    }

    /// <summary>
    /// Typed data output. Constructed with a <c>Func&lt;T&gt;</c> that produces the value on demand —
    /// usually a closure over the owning node's input ports + computed state.
    /// </summary>
    public sealed class OutputPort<T> : Port
    {
        readonly Func<T> _read;

        public OutputPort(Func<T> read) => _read = read ?? throw new ArgumentNullException(nameof(read));

        public T Read() => _read();
    }

    /// <summary>
    /// Routing endpoint — a flow exit on a <see cref="RuntimeNode"/>. Authors call
    /// <c>flow.GoTo(myFlowOut)</c> from inside <c>Execute</c>; the executor reads
    /// <see cref="Connection"/> after Execute returns and walks directly to the destination node
    /// without iterating <c>asset.flowEdges</c>.
    ///
    /// <para>Not generic over a value type — flow is routing, not value-pull. The asymmetry with
    /// <see cref="OutputPort{T}"/> is intentional: <c>OutputPort&lt;T&gt;.Read()</c> produces a
    /// typed value via a <c>Func&lt;T&gt;</c>; a flow exit produces nothing — it just marks "go
    /// here next."</para>
    /// </summary>
    public sealed class FlowOutPort : Port
    {
        public string Name { get; }
        public RuntimeNode Owner { get; }
        public FlowConnection? Connection { get; internal set; }

        public FlowOutPort(RuntimeNode owner, string name)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    /// <summary>
    /// Routing endpoint — a flow entry on a <see cref="RuntimeNode"/>. Carries no Execute-time
    /// behaviour; the executor reaches the owner via <see cref="FlowOutPort.Connection"/>'s
    /// destination ref. Present so flow wiring is symmetric with data wiring (source.OutPort ↔
    /// destination.InPort) and so validation can ask the port directly whether it's connected.
    /// </summary>
    public sealed class FlowInPort : Port
    {
        public string Name { get; }
        public RuntimeNode Owner { get; }
        public FlowConnection? Connection { get; internal set; }

        public FlowInPort(RuntimeNode owner, string name = "FlowIn")
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
