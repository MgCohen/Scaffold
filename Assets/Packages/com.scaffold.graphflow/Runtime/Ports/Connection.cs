namespace Scaffold.GraphFlow
{
    public abstract class Connection
    {
        public static Connection Bind(Port input, Port output) => input.AcceptOutput(output);
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
