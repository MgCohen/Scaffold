#nullable enable

namespace Scaffold.States
{
    /// <summary>
    /// Factory for the default <see cref="IStateEventHandler"/> implementation used by <see cref="StoreBuilder"/> when no handler is supplied.
    /// </summary>
    public static class StateEventHandlers
    {
        /// <summary>
        /// Creates a new fan-out handler instance (same behavior as the default in <see cref="StoreBuilder.Build"/>).
        /// </summary>
        public static IStateEventHandler CreateDefault()
        {
            return new StateEventHandler();
        }
    }
}
