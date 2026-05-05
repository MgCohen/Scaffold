#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class EntryRuntimeNodeBase : RuntimeNode
    {
        public abstract Type PayloadType { get; }
        public abstract Task<Flow> Run(object payload);

        public abstract void BindForRun<TRunner>(TRunner runner, GraphAsset<TRunner> asset, Func<object?>? scopeFactory)
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

        public override Type PayloadType => typeof(TEntry);
        public override Task<Flow> Run(object payload) => Run((TEntry)payload);

        public override void BindForRun<TRunner>(TRunner runner, GraphAsset<TRunner> asset, Func<object?>? scopeFactory)
        {
            BindRunner(payload =>
            {
                SetPayload(payload);
                return GraphExecutor.RunFlow(this, runner, asset, scopeFactory?.Invoke());
            });
        }
    }
}
