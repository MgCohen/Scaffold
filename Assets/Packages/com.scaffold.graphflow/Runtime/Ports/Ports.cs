using System;

namespace Scaffold.GraphFlow
{
    public abstract class Port
    {
        internal virtual Connection AcceptOutput(Port output) =>
            throw new InvalidOperationException($"Bind direction must be input.AcceptOutput(output); got {GetType()} on the input side.");
    }

    public sealed class InputPort<T> : Port
    {
        readonly Func<T>? _fallback;
        Connection<T>? _conn;

        public InputPort() { }
        public InputPort(Func<T> fallback) => _fallback = fallback;

        public T Read() => _conn != null ? _conn.Read() : (_fallback != null ? _fallback() : default!);

        internal override Connection AcceptOutput(Port output)
        {
            if (output is not OutputPort<T> typedOut)
                throw new InvalidOperationException($"Bind: output port type {output.GetType()} does not match input port type InputPort<{typeof(T)}>.");

            var conn = new Connection<T>(this, typedOut);
            _conn = conn;
            return conn;
        }
    }

    public sealed class OutputPort<T> : Port
    {
        readonly Func<T> _read;

        public OutputPort(Func<T> read) => _read = read;

        public T Read() => _read();
    }

    public sealed class FlowOutPort : Port
    {
        public string Name { get; }
        public RuntimeNode Owner { get; }
        public FlowConnection? Connection { get; internal set; }

        public FlowOutPort(RuntimeNode owner, string name)
        {
            Owner = owner;
            Name = name;
        }
    }

    public sealed class FlowInPort : Port
    {
        public string Name { get; }
        public RuntimeNode Owner { get; }
        public FlowConnection? Connection { get; internal set; }

        public FlowInPort(RuntimeNode owner, string name = "FlowIn")
        {
            Owner = owner;
            Name = name;
        }
    }
}
