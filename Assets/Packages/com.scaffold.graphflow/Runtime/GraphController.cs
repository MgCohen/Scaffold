#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public sealed class GraphController<TRunner> where TRunner : GraphRunner
    {
        readonly GraphAsset<TRunner> _asset;
        Dictionary<int, RuntimeNode> _byId = null!;
        Dictionary<Type, RuntimeNode<TRunner>> _entryRoots = null!;
        readonly GraphExecutor<TRunner> _executor = new GraphExecutor<TRunner>();
        TRunner _runner = null!;
        List<RuntimeNode> _entryNodes = new();

        /// <summary>
        /// All entry nodes discovered in the asset (anything assignable to
        /// <see cref="EntryRuntimeNode{TEntry, TRunner, TResult}"/>). Hosts pattern-match
        /// concrete generic types here to wire trigger entries into their event bus.
        /// </summary>
        public IReadOnlyList<RuntimeNode> EntryNodes => _entryNodes;

        public GraphController(GraphAsset<TRunner> asset)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        public void Initialize(TRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));

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

            _entryRoots = new Dictionary<Type, RuntimeNode<TRunner>>();
            foreach (var e in _asset.entries)
            {
                var t = Type.GetType(e.entryTypeId);
                if (t == null || !_byId.TryGetValue(e.rootNodeId, out var root))
                    continue;
                if (root is RuntimeNode<TRunner> flowRoot)
                    _entryRoots[t] = flowRoot;
            }

            // Build EntryNodes catalog + bind each entry's Run(payload) closure. The runtime node IS
            // the metadata — we walk its base chain to find EntryRuntimeNode<,,> and pull TPayload /
            // TResult off the closed generic.
            _entryNodes = new List<RuntimeNode>();
            foreach (var n in _asset.nodes)
            {
                var baseType = FindEntryBase(n.GetType());
                if (baseType == null)
                    continue;

                _entryNodes.Add(n);
                var typeArgs = baseType.GetGenericArguments(); // TEntry, TRunner, TResult
                var bindMethod = typeof(GraphController<TRunner>)
                    .GetMethod(nameof(BindEntry), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(typeArgs[0], typeArgs[2]);
                bindMethod.Invoke(this, new object[] { n });
            }
        }

        static Type? FindEntryBase(Type t)
        {
            for (var b = t; b != null; b = b.BaseType)
            {
                if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(EntryRuntimeNode<,,>))
                    return b;
            }
            return null;
        }

        void BindEntry<TEntry, TResult>(RuntimeNode node) where TEntry : class
        {
            var entry = (EntryRuntimeNode<TEntry, TRunner, TResult>)node;
            entry.BindRunner(async payload =>
            {
                entry.SetPayload(payload);
                var flow = await _executor.RunFlow(entry, _runner, _asset).ConfigureAwait(false);
                return flow.ReadResult<TResult>()!;
            });
        }

        /// <summary>
        /// Production-side typed entry invocation. Looks up the entry node by payload type, sets the
        /// payload, runs the flow, and returns the typed <typeparamref name="TResult"/>.
        /// Use <see cref="RunFlow{TEntry}"/> in tests/diagnostics when you also need
        /// <see cref="Flow.Outcome"/>.
        /// </summary>
        public async Task<TResult> Run<TEntry, TResult>(TEntry payload, CancellationToken ct = default) where TEntry : class
        {
            var flow = await RunFlow(payload, ct).ConfigureAwait(false);
            return flow.ReadResult<TResult>()!;
        }

        /// <summary>
        /// Diagnostic / test API. Returns the full <see cref="Flow"/> so callers can read
        /// <see cref="Flow.Outcome"/> in addition to the result. Production code should prefer
        /// <see cref="Run{TEntry, TResult}"/>.
        /// </summary>
        public Task<Flow> RunFlow<TEntry>(TEntry payload, CancellationToken ct = default) where TEntry : class
        {
            if (_entryRoots == null)
                throw new InvalidOperationException("Initialize must be called first.");

            var entryType = typeof(TEntry);
            if (!_entryRoots.TryGetValue(entryType, out var root))
                throw new InvalidOperationException($"No baked entry for {entryType.FullName}.");

            // Set the payload on the entry node if its closed generic includes TEntry. We can't cast
            // through the open EntryRuntimeNode<,,> here without knowing TResult, so we use reflection
            // to call SetPayload via the closed base.
            var baseType = FindEntryBase(root.GetType());
            if (baseType != null)
            {
                var setPayload = baseType.GetMethod("SetPayload");
                setPayload?.Invoke(root, new object[] { payload });
            }

            return _executor.RunFlow(root, _runner, _asset, ct);
        }

        public void Dispose()
        {
            _byId?.Clear();
            _entryRoots?.Clear();
            _entryNodes?.Clear();
        }
    }
}
