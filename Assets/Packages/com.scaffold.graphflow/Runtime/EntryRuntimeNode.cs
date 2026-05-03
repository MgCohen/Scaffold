namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Base for generated/hand-written entry runtime nodes — receives the typed dispatch payload from the controller.
    /// </summary>
    // TODO M3 phase 3: gain TResult per D5/D9 — promote to EntryRuntimeNode&lt;TEntry, TRunner, TResult&gt;
    // and add Task&lt;TResult&gt; Run(TEntry) so hosts can pattern-match the entry list and invoke without
    // restating type args. Lands together with the generator emit update so editor + runtime stay in sync.
    public abstract class EntryRuntimeNode<TEntry, TRunner> : RuntimeNode<TRunner>
        where TEntry : class
        where TRunner : GraphRunner
    {
        protected TEntry? Payload { get; private set; }

        public void SetPayload(TEntry payload) => Payload = payload;
    }
}
