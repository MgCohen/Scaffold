#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    public abstract class GraphRunner
    {
        public IReadOnlyList<RuntimeNode> Nodes { get; }
        public IReadOnlyDictionary<Type, EntryRuntimeNodeBase> EntriesByPayload { get; }
        public IVariableBag Variables { get; internal set; } = null!;

        // Cap on concurrent flows running against this runner. Each Flow
        // takes one index from this pool for its lifetime; Complete() returns
        // it. Every cached output port allocates an Entry[MaxConcurrentFlows],
        // so raising this costs memory linearly across cached ports.
        public int MaxConcurrentFlows { get; }
        readonly Stack<int> _freeFlowIndices;
        int _versionCounter;

        protected GraphRunner(BakedGraph baked, int maxConcurrentFlows = 8)
        {
            if (maxConcurrentFlows < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentFlows),
                    maxConcurrentFlows,
                    "MaxConcurrentFlows must be at least 1.");
            Nodes = baked.Nodes;
            EntriesByPayload = baked.EntriesByPayload;
            MaxConcurrentFlows = maxConcurrentFlows;
            _freeFlowIndices = new Stack<int>(maxConcurrentFlows);
            // Push in reverse so first Acquire returns 0; deterministic for tests.
            for (int i = maxConcurrentFlows - 1; i >= 0; i--)
                _freeFlowIndices.Push(i);
        }

        internal int AcquireFlowIndex()
        {
            if (_freeFlowIndices.Count == 0)
                throw new InvalidOperationException(
                    $"GraphRunner: MaxConcurrentFlows={MaxConcurrentFlows} exhausted. "
                    + "Raise the cap by passing maxConcurrentFlows to base(...).");
            return _freeFlowIndices.Pop();
        }

        internal void ReleaseFlowIndex(int index)
        {
            _freeFlowIndices.Push(index);
        }

        // Monotonic per-runner version source for OutputPort cache freshness.
        internal int NextCacheVersion() => ++_versionCounter;

        /// <summary>Override to fully construct the runner's variable bag —
        /// seed the graph-declared variables and chain to whatever consumer
        /// storage you want (shared variables, save state, network store,
        /// state-backed bag, ...). Per Invariant 2, the consumer owns bag
        /// construction so graph-declared variables can flow through any
        /// custom storage instead of always landing in an in-memory bag.
        /// Default materializes everything in-memory with no parent.</summary>
        protected internal virtual IVariableBag CreateVariableBag(IEnumerable<RuntimeVariable> seed)
            => CreateInMemoryBag(seed);

        /// <summary>Convenience for overrides that still want the standard
        /// in-memory materialization but need a different parent (e.g. a
        /// consumer-supplied global bag).</summary>
        protected static InMemoryVariableBag CreateInMemoryBag(
            IEnumerable<RuntimeVariable> seed, IVariableBag? parent = null)
        {
            var bag = new InMemoryVariableBag(parent);
            if (seed == null) return bag;
            foreach (var v in seed)
            {
                if (v == null || string.IsNullOrEmpty(v.id) || v.defaultValue == null) continue;
                bag.Add(v.defaultValue.CreateHandle(v.id));
            }
            return bag;
        }

        public virtual void Initialize() { }

        public Task<Flow<TEntry>> Run<TEntry>(TEntry payload, CancellationToken ct = default)
            where TEntry : class
        {
            if (!EntriesByPayload.TryGetValue(typeof(TEntry), out var entry))
                throw new InvalidOperationException(
                    $"No entry for {typeof(TEntry).FullName}.");

            var flow = NewFlow(payload, ct);
            return RunFromEntry(entry, flow);
        }

        protected async Task<TResult?> RunFromFlowOut<TPayload, TResult>(
            TPayload payload, FlowOutPort flowOut, CancellationToken ct = default)
            where TPayload : class
        {
            var flow = NewFlow(payload, ct);
            try
            {
                var dest = flowOut.Connection?.Destination;
                if (dest != null) await RunFromInPort(dest, flow);
                flow.InvalidateAll();
                return flow.ReadResult<TResult>();
            }
            finally { flow.Complete(); }
        }

        // Used by ObserveVariable<T> on handle.Subscribe to drive a flow from the
        // observer's FlowOut. Internal to avoid widening the public surface for
        // arbitrary "drive a flow from X" needs — observers go through this seam.
        //
        // Observers are fire-and-forget at the call site (`_ = runner.RunObserver(...)`),
        // so any unhandled exception in the spawned flow would otherwise be lost in
        // the discarded Task and only resurface as UnobservedTaskException at GC
        // time. Catch and log here so failures show up immediately with their
        // original stack — without making the public surface awkward.
        internal async Task RunObserver<TPayload>(FlowOutPort flowOut, TPayload payload, CancellationToken ct = default)
            where TPayload : class
        {
            Flow<TPayload>? flow = null;
            try
            {
                flow = NewFlow(payload, ct);
                var dest = flowOut.Connection?.Destination;
                if (dest != null) await RunFromInPort(dest, flow);
                flow.InvalidateAll();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
            finally
            {
                flow?.Complete();
            }
        }

        Flow<TPayload> NewFlow<TPayload>(TPayload payload, CancellationToken ct) where TPayload : class =>
            new Flow<TPayload>(payload, this, ct);

        async Task<Flow<TEntry>> RunFromEntry<TEntry>(EntryRuntimeNodeBase entry, Flow<TEntry> flow)
            where TEntry : class
        {
            try
            {
                var defaultOut = entry.GetDefaultOut();
                var dest = defaultOut.Connection?.Destination;
                if (dest != null) await RunFromInPort(dest, flow);
                flow.InvalidateAll();
                return flow;
            }
            finally { flow.Complete(); }
        }

        async Task RunFromInPort(FlowInPort start, Flow flow)
        {
            FlowInPort? current = start;
            while (current != null)
            {
                // Honor flow.Cancel() / flow.Return() even if the calling node
                // forgot to return null from its FlowIn handler. Returns null
                // are still the primary termination signal — this is the
                // belt-and-braces check so the runtime contract matches the
                // intuitive "Cancel stops execution" semantics.
                if (flow.Outcome != Outcome.Running) break;
                var next = await current.Invoke(flow);
                if (next == null) break;
                current = next.Connection?.Destination;
            }
        }
    }
}
