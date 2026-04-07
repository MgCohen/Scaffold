namespace Scaffold.States
{
    /// <summary>
    /// Describes how a canonical slice row changed for <see cref="IStateEventHandler.Notify"/> subscribers.
    /// </summary>
    public enum StateChangeEvent
    {
        /// <summary>A new canonical row was registered (for example <see cref="Store.RegisterSlice"/>).</summary>
        Created,

        /// <summary>An existing row received new committed state (mutators, snapshot apply, aggregate rebuild).</summary>
        Updated,

        /// <summary>A canonical row was removed (<see cref="Store.UnregisterSlice"/>, snapshot prune).</summary>
        Removed,
    }
}
