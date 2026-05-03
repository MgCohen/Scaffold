using System;

namespace Scaffold.GraphFlow.M0
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
    /// Flow-output marker — the only port that carries an id at runtime, because
    /// <c>RuntimeNode&lt;TRunner&gt;.Execute</c> returns a <see cref="FlowContinuation"/> that names
    /// which flow-out the executor should follow. Flow inputs are editor-only metadata and have no
    /// runtime <see cref="Port"/> object: the executor calls <c>Execute</c> on the destination node
    /// directly, walking <c>flowEdges</c>.
    /// </summary>
    public readonly struct FlowOut
    {
        readonly int _portId;

        public FlowOut(int portId) => _portId = portId;

        public FlowContinuation Continue() => FlowContinuation.Next(_portId);
    }
}
