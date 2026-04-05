namespace Scaffold.States
{
    /// <summary>
    /// Canonical slice payload: mutators may commit instances of this type; aggregates do not inherit it.
    /// </summary>
    public abstract record State : BaseState
    {
    }
}
