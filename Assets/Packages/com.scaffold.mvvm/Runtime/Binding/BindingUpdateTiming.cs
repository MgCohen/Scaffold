namespace Scaffold.MVVM.Binding
{
    /// <summary>
    /// When binding refresh runs relative to <see cref="IDeferredBindingScheduler"/> (for non-immediate modes).
    /// </summary>
    public enum BindingUpdateTiming
    {
        /// <summary>
        /// Run getter and target update synchronously on each <see cref="IBindings.UpdateBind"/> call.
        /// </summary>
        Immediate = 0,

        /// <summary>
        /// Defer target updates; the registered scheduler decides when (typically next frame).
        /// </summary>
        NextFrame = 1,

        /// <summary>
        /// Defer target updates; the registered scheduler decides when (typically after frame rendering in Unity).
        /// </summary>
        EndOfFrame = 2,
    }
}
