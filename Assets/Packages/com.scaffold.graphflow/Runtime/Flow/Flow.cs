#nullable enable
using System.Threading;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    public enum Outcome { Running, Returned, Cancelled }

    // Contravariant: a Flow<*, Spell> implements IFlowResult<FireSpell> too,
    // so Return<FireSpell> writes through correctly when the caller asked for
    // Spell. Unrelated types fail the cast and throw InvalidCastException.
    internal interface IFlowResult<in TResult>
    {
        void SetResult(TResult value);
    }

    public class Flow
    {
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

        public FlowOutPort? Return()
        {
            Outcome = Outcome.Returned;
            return null;
        }

        public FlowOutPort? Cancel()
        {
            Outcome = Outcome.Cancelled;
            return null;
        }

        public void InvalidateAll() => CacheVersion = Runner.NextCacheVersion();

        internal void Complete() => Runner.ReleaseFlowIndex(Index);
    }

    public class Flow<TPayload> : Flow where TPayload : class
    {
        public TPayload Payload { get; }

        internal Flow(TPayload payload, GraphRunner runner, CancellationToken token)
            : base(runner, token)
        {
            Payload = payload;
        }
    }

    public sealed class Flow<TPayload, TResult> : Flow<TPayload>, IFlowResult<TResult>
        where TPayload : class
    {
        public TResult Result { get; private set; } = default!;

        // Explicit implementation — Result is read-only from outside; only
        // Return<TResult> (via the IFlowResult cast) can write.
        void IFlowResult<TResult>.SetResult(TResult value) => Result = value;

        internal Flow(TPayload payload, GraphRunner runner, CancellationToken token)
            : base(payload, runner, token) { }
    }
}
