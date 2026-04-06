#nullable enable

namespace Scaffold.States
{
    /// <summary>
    /// How buffered <see cref="IStateEventHandler.Notify"/> calls are merged before the inner handler runs them on <see cref="IStateEventDeferralController.Flush"/>.
    /// </summary>
    public enum StateEventMergeMode
    {
        /// <summary>
        /// Replay every notification in order (one inner <c>Notify</c> per buffered call).
        /// </summary>
        PreserveAll,

        /// <summary>
        /// At most one inner notification per distinct <c>(reference, state type)</c> key; the last state wins for that key. Order follows first-seen key order.
        /// </summary>
        LatestPerKey,
    }
}
