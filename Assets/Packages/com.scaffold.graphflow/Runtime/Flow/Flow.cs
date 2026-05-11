#nullable enable
using System.Collections.Generic;
using System.Threading;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    public enum Outcome { Running, Returned, Cancelled }

    public sealed class Flow
    {
        // Monotonic, process-wide. Bumped on every Flow construction and every
        // InvalidateAll so cached entries from any prior version are detectable
        // by a simple int compare. OutputPort<T>'s per-flow cache uses this as
        // its freshness key: an entry with Version != flow.CacheVersion is stale.
        static int s_globalVersion;

        readonly object _payload;
        Dictionary<object, object>? _slots;
        object? _result;
        IVariableBag? _variables;
        bool _indexReleased;

        public GraphRunner Runner { get; }
        public CancellationToken Token { get; }

        // Pool-assigned slot index into per-port caches. Owned for the flow's
        // active lifetime; Complete() returns it to the runner pool.
        public int Index { get; }

        // Freshness key paired with each cache Entry. InvalidateAll bumps it
        // via Interlocked.Increment on s_globalVersion so post-invalidate
        // reads always see a fresh number that no earlier write could match.
        public int CacheVersion { get; private set; }

        public IVariableBag Variables =>
            _variables ??= new InMemoryVariableBag(Runner.Variables);

        public Outcome Outcome { get; private set; } = Outcome.Running;
        public bool IsCancelled => Outcome == Outcome.Cancelled;
        public bool IsTerminating => Outcome != Outcome.Running;

        internal Flow(object payload, GraphRunner runner, CancellationToken token)
        {
            _payload = payload;
            Runner = runner;
            Token = token;
            Index = runner.AcquireFlowIndex();
            CacheVersion = Interlocked.Increment(ref s_globalVersion);
        }

        public T? GetPayload<T>() where T : class => _payload as T;

        public FlowOutPort? Return<T>(T value)
        {
            _result = value;
            Outcome = Outcome.Returned;
            return null;
        }

        public FlowOutPort? Return()
        {
            _result = null;
            Outcome = Outcome.Returned;
            return null;
        }

        public FlowOutPort? Cancel()
        {
            Outcome = Outcome.Cancelled;
            return null;
        }

        public T? ReadResult<T>() => _result is T t ? t : default;

        public void InvalidateAll()
        {
            CacheVersion = Interlocked.Increment(ref s_globalVersion);
        }

        // Returns the flow's slot index to the runner's pool. Called by the
        // runner at the end of every run path. Idempotent — late calls after
        // a manual Complete() are no-ops.
        internal void Complete()
        {
            if (_indexReleased) return;
            _indexReleased = true;
            Runner.ReleaseFlowIndex(Index);
        }

        public T GetSlot<T>(object owner) =>
            _slots != null && _slots.TryGetValue(owner, out var v) ? (T)v : default!;

        public void SetSlot<T>(object owner, T value) =>
            (_slots ??= new())[owner] = value!;
    }
}
