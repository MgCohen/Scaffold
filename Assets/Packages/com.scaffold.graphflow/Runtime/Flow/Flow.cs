#nullable enable
using System.Threading;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    public enum Outcome { Running, Returned, Cancelled }

    public class Flow
    {
        object? _result;
        IVariableBag? _variables;

        public GraphRunner Runner { get; }
        public CancellationToken Token { get; }

        // Index into the runner's per-port caches; returned to the pool on Complete.
        public int Index { get; }

        // Per-flow freshness key; OutputPort entries are stale when their Version differs.
        internal int CacheVersion { get; private set; }

        public IVariableBag Variables =>
            _variables ??= new InMemoryVariableBag(Runner.Variables);

        public Outcome Outcome { get; private set; } = Outcome.Running;

        internal Flow(GraphRunner runner, CancellationToken token)
        {
            Runner = runner;
            Token = token;
            Index = runner.AcquireFlowIndex();
            CacheVersion = runner.NextCacheVersion();
        }

        public FlowOutPort? Return<T>(T value)
        {
            _result = value;
            Outcome = Outcome.Returned;
            return null;
        }

        public FlowOutPort? Return() => Return<object?>(null);

        public FlowOutPort? Cancel()
        {
            Outcome = Outcome.Cancelled;
            return null;
        }

        public T? ReadResult<T>() => _result is T t ? t : default;

        public void InvalidateAll() => CacheVersion = Runner.NextCacheVersion();

        internal void Complete() => Runner.ReleaseFlowIndex(Index);
    }

    public sealed class Flow<TPayload> : Flow where TPayload : class
    {
        public TPayload Payload { get; }

        internal Flow(TPayload payload, GraphRunner runner, CancellationToken token)
            : base(runner, token)
        {
            Payload = payload;
        }
    }
}
