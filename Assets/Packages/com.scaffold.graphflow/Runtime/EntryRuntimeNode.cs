#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Non-generic-on-payload base. Controller dispatches on this to create per-payload
    /// <see cref="IEntryBridge"/> instances without reflection.
    /// </summary>
    public abstract class EntryRuntimeNodeBase<TRunner> : RuntimeNode<TRunner> where TRunner : GraphRunner
    {
        public abstract IEntryBridge CreateBridge(TRunner runner, GraphAsset<TRunner> asset, GraphExecutor<TRunner> executor, Func<IEffectScope?>? scopeFactory);
    }

    /// <summary>
    /// Base for generated/hand-written entry runtime nodes. Carries <c>TPayload</c> for routing
    /// and <c>TRunner</c> for the executor binding. Provides a default <see cref="CreateBridge"/>
    /// that constructs a typed <see cref="EntryBridge{TEntry, TRunner}"/> — generator-emitted entries
    /// inherit it for free; hand-authored entries do too.
    /// </summary>
    public abstract class EntryRuntimeNode<TEntry, TRunner> : EntryRuntimeNodeBase<TRunner>
        where TEntry : class
        where TRunner : GraphRunner
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

        public override IEntryBridge CreateBridge(TRunner runner, GraphAsset<TRunner> asset, GraphExecutor<TRunner> executor, Func<IEffectScope?>? scopeFactory)
            => new EntryBridge<TEntry, TRunner>(this, runner, asset, executor, scopeFactory);
    }

    /// <summary>
    /// Generic per-payload bridge. Closes over the entry node + executor + asset + runner so the
    /// controller can dispatch by payload type without reflection. The closure also wires
    /// <see cref="EntryRuntimeNode{TEntry, TRunner}.BindRunner"/> so hosts that pattern-match the
    /// entry can call <c>Run(payload)</c> directly.
    /// </summary>
    public sealed class EntryBridge<TEntry, TRunner> : IEntryBridge
        where TEntry : class
        where TRunner : GraphRunner
    {
        readonly EntryRuntimeNode<TEntry, TRunner> _node;
        readonly TRunner _runner;
        readonly GraphAsset<TRunner> _asset;
        readonly GraphExecutor<TRunner> _executor;
        readonly Func<IEffectScope?>? _scopeFactory;

        public EntryBridge(EntryRuntimeNode<TEntry, TRunner> node, TRunner runner, GraphAsset<TRunner> asset, GraphExecutor<TRunner> executor, Func<IEffectScope?>? scopeFactory)
        {
            _node = node;
            _runner = runner;
            _asset = asset;
            _executor = executor;
            _scopeFactory = scopeFactory;

            _node.BindRunner(payload =>
            {
                _node.SetPayload(payload);
                return _executor.RunFlow(_node, _runner, _asset, _scopeFactory?.Invoke());
            });
        }

        public Type PayloadType => typeof(TEntry);

        public Task<Flow> Run(object payload)
        {
            _node.SetPayload((TEntry)payload);
            return _executor.RunFlow(_node, _runner, _asset, _scopeFactory?.Invoke());
        }
    }
}
