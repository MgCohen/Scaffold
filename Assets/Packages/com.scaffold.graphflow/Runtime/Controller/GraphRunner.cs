#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public abstract class GraphRunner
    {
        public IReadOnlyList<RuntimeNode> Nodes { get; }
        public IReadOnlyDictionary<Type, EntryRuntimeNodeBase> EntriesByPayload { get; }
        public IVariableBag Variables { get; private set; } = null!;

        protected GraphRunner(BakedGraph baked)
        {
            Nodes = baked.Nodes;
            EntriesByPayload = baked.EntriesByPayload;
        }

        /// <summary>Override to inject the outermost (parent / global) bag —
        /// shared variables, save state, network store, entities VariableBag
        /// adapter, ... GraphFlow runtime never references the consumer's types.</summary>
        protected virtual IVariableBag? CreateParentBag() => null;

        internal void SeedVariables(IReadOnlyList<RuntimeVariable> seed)
            => Variables = new InMemoryVariableBag(seed, CreateParentBag());

        public virtual void Initialize() { }

        public Task<Flow> Run<TEntry>(TEntry payload, CancellationToken ct = default)
            where TEntry : class
        {
            if (!EntriesByPayload.TryGetValue(typeof(TEntry), out var entry))
                throw new InvalidOperationException(
                    $"No entry for {typeof(TEntry).FullName}.");

            var flow = NewFlow(payload, ct);
            return RunFromEntry(entry, flow);
        }

        protected async Task<TResult?> RunFromFlowOut<TResult>(
            object payload, FlowOutPort flowOut, CancellationToken ct = default)
        {
            var flow = NewFlow(payload, ct);
            var dest = flowOut.Connection?.Destination;
            if (dest != null) await RunFromInPort(dest, flow);
            flow.InvalidateAll();
            return flow.ReadResult<TResult>();
        }

        Flow NewFlow(object payload, CancellationToken ct) => new Flow(payload, this, ct);

        async Task<Flow> RunFromEntry(EntryRuntimeNodeBase entry, Flow flow)
        {
            var defaultOut = entry.GetDefaultOut();
            var dest = defaultOut.Connection?.Destination;
            if (dest != null) await RunFromInPort(dest, flow);
            flow.InvalidateAll();
            return flow;
        }

        async Task RunFromInPort(FlowInPort start, Flow flow)
        {
            FlowInPort? current = start;
            while (current != null)
            {
                var next = await current.Invoke(flow).ConfigureAwait(false);
                if (next == null) break;
                current = next.Connection?.Destination;
            }
        }
    }
}
