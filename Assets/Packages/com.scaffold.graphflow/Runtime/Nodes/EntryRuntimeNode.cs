#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class EntryRuntimeNodeBase : RuntimeNode
    {
        public abstract IEntryBridge CreateBridge<TRunner>(TRunner runner, GraphAsset<TRunner> asset, Func<object?>? scopeFactory)
            where TRunner : GraphRunner;
    }

    [Serializable]
    public abstract class EntryRuntimeNode<TEntry> : EntryRuntimeNodeBase
        where TEntry : class
    {
        protected TEntry? Payload { get; private set; }
        public void SetPayload(TEntry payload) => Payload = payload;

        Func<TEntry, Task<Flow>>? _runFromHere;
        public void BindRunner(Func<TEntry, Task<Flow>> runFromHere) => _runFromHere = runFromHere;

        public Task<Flow> Run(TEntry payload)
        {
            if (_runFromHere == null) throw new InvalidOperationException("Entry not initialized.");
            return _runFromHere(payload);
        }

        public override IEntryBridge CreateBridge<TRunner>(TRunner runner, GraphAsset<TRunner> asset, Func<object?>? scopeFactory)
            => new EntryBridge<TEntry, TRunner>(this, runner, asset, scopeFactory);
    }

    public sealed class EntryBridge<TEntry, TRunner> : IEntryBridge
        where TEntry : class
        where TRunner : GraphRunner
    {
        readonly EntryRuntimeNode<TEntry> _node;
        readonly TRunner _runner;
        readonly GraphAsset<TRunner> _asset;
        readonly Func<object?>? _scopeFactory;

        public EntryBridge(EntryRuntimeNode<TEntry> node, TRunner runner, GraphAsset<TRunner> asset, Func<object?>? scopeFactory)
        {
            _node = node;
            _runner = runner;
            _asset = asset;
            _scopeFactory = scopeFactory;

            _node.BindRunner(payload =>
            {
                _node.SetPayload(payload);
                return GraphExecutor.RunFlow(_node, _runner, _asset, _scopeFactory?.Invoke());
            });
        }

        public Type PayloadType => typeof(TEntry);

        public Task<Flow> Run(object payload)
        {
            _node.SetPayload((TEntry)payload);
            return GraphExecutor.RunFlow(_node, _runner, _asset, _scopeFactory?.Invoke());
        }
    }
}
