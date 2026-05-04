#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public sealed class GraphController<TRunner> where TRunner : GraphRunner
    {
        readonly GraphAsset<TRunner> _asset;
        Dictionary<int, RuntimeNode> _byId = null!;
        Dictionary<Type, IEntryBridge> _bridges = new();
        readonly GraphExecutor<TRunner> _executor = new GraphExecutor<TRunner>();
        TRunner _runner = null!;
        List<RuntimeNode> _entryNodes = new();
        Func<IEffectScope?>? _scopeFactory;

        /// <summary>
        /// All entry nodes discovered in the asset (anything assignable to
        /// <see cref="EntryRuntimeNode{TEntry, TRunner}"/>). Hosts pattern-match
        /// concrete generic types here to wire trigger entries into their event bus.
        /// </summary>
        public IReadOnlyList<RuntimeNode> EntryNodes => _entryNodes;

        public GraphController(GraphAsset<TRunner> asset)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        public void Initialize(TRunner runner, Func<IEffectScope?>? scopeFactory = null)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _scopeFactory = scopeFactory;

            _byId = new Dictionary<int, RuntimeNode>(_asset.nodes.Count);
            foreach (var n in _asset.nodes)
                _byId[n.nodeId] = n;

            // Hydrate data wiring through the single Connection.Bind seam. Per-node Bind on the base
            // looks up both ports via the dict and constructs Connection<T>.
            foreach (var c in _asset.connections)
            {
                if (!_byId.TryGetValue(c.fromNodeId, out var from) || !_byId.TryGetValue(c.toNodeId, out var to))
                    continue;

                to.Bind(c.toPortId, from, c.fromPortId);
            }

            // Build EntryNodes catalog + per-payload bridges. The generator emits a CreateBridge
            // override per concrete EntryRuntimeNode; we dispatch via the non-generic
            // EntryRuntimeNodeBase<TRunner> abstract — zero reflection.
            _entryNodes = new List<RuntimeNode>();
            _bridges.Clear();
            foreach (var n in _asset.nodes)
            {
                if (n is not EntryRuntimeNodeBase<TRunner> entry)
                    continue;

                _entryNodes.Add(n);
                var bridge = entry.CreateBridge(_runner, _asset, _executor, _scopeFactory);
                _bridges[bridge.PayloadType] = bridge;
            }
        }

        /// <summary>
        /// Typed entry invocation. Looks up the entry bridge by payload type, sets the payload, runs
        /// the flow, and returns the resulting <see cref="Flow"/> (callers read
        /// <see cref="Flow.Outcome"/> / <see cref="Flow.ReadResult{T}"/> as needed).
        /// </summary>
        public Task<Flow> Run<TEntry>(TEntry payload, CancellationToken ct = default) where TEntry : class
        {
            if (_bridges == null)
                throw new InvalidOperationException("Initialize must be called first.");

            var entryType = typeof(TEntry);
            if (!_bridges.TryGetValue(entryType, out var bridge))
                throw new InvalidOperationException($"No baked entry for {entryType.FullName}.");

            return bridge.Run(payload);
        }

        public void Dispose()
        {
            _byId?.Clear();
            _bridges?.Clear();
            _entryNodes?.Clear();
        }
    }
}
