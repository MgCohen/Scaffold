#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Base for generated/hand-written entry runtime nodes. Carries <c>TPayload</c> for routing,
    /// <c>TRunner</c> for the executor binding, and <c>TResult</c> as the typed return shape.
    /// <para>
    /// During <see cref="GraphController{TRunner}.Initialize"/> the controller calls
    /// <see cref="BindRunner"/> with a closure that constructs a <see cref="Flow"/>, sets the payload,
    /// runs the executor, and returns the typed result. Hosts can then pattern-match the entry list
    /// from <c>controller.EntryNodes</c> and call <see cref="Run"/> directly without restating type args.
    /// </para>
    /// </summary>
    public abstract class EntryRuntimeNode<TEntry, TRunner, TResult> : RuntimeNode<TRunner>
        where TEntry : class
        where TRunner : GraphRunner
    {
        protected TEntry? Payload { get; private set; }
        public void SetPayload(TEntry payload) => Payload = payload;

        Func<TEntry, Task<TResult>>? _runFromHere;
        internal void BindRunner(Func<TEntry, Task<TResult>> runFromHere) => _runFromHere = runFromHere;

        public Task<TResult> Run(TEntry payload)
        {
            if (_runFromHere == null) throw new InvalidOperationException("Entry not initialized.");
            return _runFromHere(payload);
        }
    }
}
