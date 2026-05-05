#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Non-generic base for entry runtime nodes — used as the controller's dispatch surface for
    /// payload-typed bridges, without exposing TPayload to the controller's loop.
    /// </summary>
    [Serializable]
    public abstract class EntryRuntimeNodeBase : RuntimeNode
    {
        public abstract IEntryBridge CreateBridge<TRunner>(TRunner runner, GraphAsset<TRunner> asset, GraphExecutor<TRunner> executor, Func<object?>? scopeFactory)
            where TRunner : GraphRunner;
    }

    /// <summary>
    /// Base for generated/hand-written entry runtime nodes. Carries <c>TPayload</c> for routing only —
    /// the runner reference is plumbed into the bridge at <see cref="CreateBridge"/> time, not into
    /// the Execute body. Entries that genuinely need typed runner access cast <c>flow.Runner</c> in
    /// their <see cref="RuntimeNode.Execute"/> override (same pattern Mode-2 dispatchers use for
    /// <c>flow.Scope</c>).
    ///
    /// <para>Post-M3 phase 2 (decision #5 + finish of decision #1): TRunner dropped. Single-T form
    /// only — the prior 2-arg <c>EntryRuntimeNode&lt;TEntry, TRunner&gt;</c> is removed.</para>
    /// </summary>
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

        public override IEntryBridge CreateBridge<TRunner>(TRunner runner, GraphAsset<TRunner> asset, GraphExecutor<TRunner> executor, Func<object?>? scopeFactory)
            => new EntryBridge<TEntry, TRunner>(this, runner, asset, executor, scopeFactory);
    }

    /// <summary>
    /// Generic per-payload bridge. Closes over the entry node + executor + asset + runner so the
    /// controller can dispatch by payload type without reflection. The closure also wires
    /// <see cref="EntryRuntimeNode{TEntry}.BindRunner"/> so hosts that pattern-match the entry can
    /// call <c>Run(payload)</c> directly.
    /// </summary>
    public sealed class EntryBridge<TEntry, TRunner> : IEntryBridge
        where TEntry : class
        where TRunner : GraphRunner
    {
        readonly EntryRuntimeNode<TEntry> _node;
        readonly TRunner _runner;
        readonly GraphAsset<TRunner> _asset;
        readonly GraphExecutor<TRunner> _executor;
        readonly Func<object?>? _scopeFactory;

        public EntryBridge(EntryRuntimeNode<TEntry> node, TRunner runner, GraphAsset<TRunner> asset, GraphExecutor<TRunner> executor, Func<object?>? scopeFactory)
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
