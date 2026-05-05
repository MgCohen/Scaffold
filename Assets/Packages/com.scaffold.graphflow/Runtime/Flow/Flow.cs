#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public enum FlowOutcome { Stopped, Returned, Cancelled }

    public sealed class Flow
    {
        public CancellationToken CancellationToken { get; }
        public string? Reason { get; set; }

        public FlowOutcome Outcome { get; private set; }
        internal object? Result { get; private set; }
        FlowOutPort? _nextPort;

        public object? Scope { get; internal set; }

        public GraphRunner? Runner { get; internal set; }

        public Flow(CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
        }

        public Task GoTo(FlowOutPort port)
        {
            _nextPort = port;
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            Outcome = FlowOutcome.Stopped;
            _nextPort = null;
            return Task.CompletedTask;
        }

        public Task Return<T>(T value)
        {
            Outcome = FlowOutcome.Returned;
            Result = value;
            _nextPort = null;
            return Task.CompletedTask;
        }

        public Task Return()
        {
            Outcome = FlowOutcome.Returned;
            Result = null;
            _nextPort = null;
            return Task.CompletedTask;
        }

        public Task Cancel()
        {
            Outcome = FlowOutcome.Cancelled;
            _nextPort = null;
            return Task.CompletedTask;
        }

        internal FlowOutPort? ConsumeNext()
        {
            var n = _nextPort;
            _nextPort = null;
            return n;
        }

        public T? ReadResult<T>() => Result is T t ? t : default;
    }
}
