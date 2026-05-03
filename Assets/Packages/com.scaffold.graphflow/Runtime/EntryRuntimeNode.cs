namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Base for generated/hand-written entry runtime nodes — receives the typed dispatch payload from the controller.
    /// </summary>
    public abstract class EntryRuntimeNode<TEntry, TRunner> : RuntimeNode<TRunner>
        where TEntry : class
        where TRunner : GraphRunner
    {
        protected TEntry? Payload { get; private set; }

        public void SetPayload(TEntry payload) => Payload = payload;
    }
}
