namespace Scaffold.States
{
    /// <summary>
    /// Marker base for derived aggregates built from canonical slices (read-only through mutators).
    /// </summary>
    public abstract record AggregateState : BaseState
    {
    }
}
