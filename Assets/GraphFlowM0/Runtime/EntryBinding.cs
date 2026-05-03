namespace Scaffold.GraphFlow.M0
{
    /// <summary>Optional hook for runtime nodes that need the concrete entry payload before Execute.</summary>
    public interface IBindsGraphEntryPayload
    {
        void BindGraphEntryPayload(object payload);
    }
}
