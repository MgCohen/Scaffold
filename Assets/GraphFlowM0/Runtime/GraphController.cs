using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.M0
{
    public sealed class GraphController<TRunner> where TRunner : GraphRunner
    {
        readonly GraphAsset<TRunner> _asset;
        Dictionary<int, RuntimeNode<TRunner>> _byId = null!;
        Dictionary<Type, RuntimeNode<TRunner>> _entryRoots = null!;
        readonly GraphExecutor<TRunner> _executor = new GraphExecutor<TRunner>();
        TRunner _runner = null!;

        public GraphController(GraphAsset<TRunner> asset)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        public void Initialize(TRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));

            _byId = new Dictionary<int, RuntimeNode<TRunner>>(_asset.nodes.Count);
            foreach (var n in _asset.nodes)
                _byId[n.nodeId] = n;

            foreach (var c in _asset.connections)
            {
                if (!_byId.TryGetValue(c.fromNodeId, out var from) || !_byId.TryGetValue(c.toNodeId, out var to))
                    continue;

                var conn = from.GetOutputConnection(c.fromPortId);
                to.BindInput(c.toPortId, conn);
            }

            _entryRoots = new Dictionary<Type, RuntimeNode<TRunner>>();
            foreach (var e in _asset.entries)
            {
                var t = Type.GetType(e.entryTypeId);
                if (t == null || !_byId.TryGetValue(e.rootNodeId, out var root))
                    continue;
                _entryRoots[t] = root;
            }
        }

        public Task Run<TEntry>(TEntry payload) where TEntry : class
        {
            if (_entryRoots == null)
                throw new InvalidOperationException("Initialize must be called first.");

            var entryType = typeof(TEntry);
            if (!_entryRoots.TryGetValue(entryType, out var root))
                throw new InvalidOperationException($"No baked entry for {entryType.FullName}.");

            if (root is EntryRuntimeNode<TEntry, TRunner> entryRoot)
                entryRoot.SetPayload(payload);

            return _executor.RunFlow(root, _runner, _asset);
        }

        public void Dispose()
        {
            _byId?.Clear();
            _entryRoots?.Clear();
        }
    }
}
