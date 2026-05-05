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
        TRunner _runner = null!;
        List<RuntimeNode> _entryNodes = new();
        Func<object?>? _scopeFactory;

        public IReadOnlyList<RuntimeNode> EntryNodes => _entryNodes;

        public GraphController(GraphAsset<TRunner> asset)
        {
            _asset = asset;
        }

        public void Initialize(TRunner runner, Func<object?>? scopeFactory = null)
        {
            _runner = runner;
            _scopeFactory = scopeFactory;

            _byId = new Dictionary<int, RuntimeNode>(_asset.nodes.Count);
            foreach (var n in _asset.nodes)
                _byId[n.nodeId] = n;

            foreach (var c in _asset.connections)
            {
                if (!_byId.TryGetValue(c.fromNodeId, out var from) || !_byId.TryGetValue(c.toNodeId, out var to))
                    continue;

                to.Bind(c.toPortName, from, c.fromPortName);
            }

            foreach (var e in _asset.flowEdges)
            {
                if (!_byId.TryGetValue(e.fromNodeId, out var from) || !_byId.TryGetValue(e.toNodeId, out var to))
                    continue;
                if (!from.Ports.TryGetValue(e.fromPortName, out var srcPort) || srcPort is not FlowOutPort flowOut)
                    continue;
                if (!to.Ports.TryGetValue(e.toPortName, out var dstPort) || dstPort is not FlowInPort flowIn)
                    continue;

                var connection = new FlowConnection(flowOut, flowIn);
                flowOut.Connection = connection;
                flowIn.Connection = connection;
            }

            foreach (var n in _asset.nodes)
            {
                if (n is RuntimeNode<TRunner> typed)
                    typed.BindRunner(_runner);
            }

            _entryNodes = new List<RuntimeNode>();
            _bridges.Clear();
            foreach (var n in _asset.nodes)
            {
                if (n is not EntryRuntimeNodeBase entry)
                    continue;

                _entryNodes.Add(n);
                var bridge = entry.CreateBridge<TRunner>(_runner, _asset, _scopeFactory);
                _bridges[bridge.PayloadType] = bridge;
            }
        }

        public Task<Flow> Run<TEntry>(TEntry payload, CancellationToken ct = default) where TEntry : class
        {
            var entryType = typeof(TEntry);
            if (!_bridges.TryGetValue(entryType, out var bridge))
                throw new InvalidOperationException($"No baked entry for {entryType.FullName}.");

            return bridge.Run(payload);
        }
    }
}
