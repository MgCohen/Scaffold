namespace Scaffold.MVVM.Binding
{
    /// <summary>
    /// When binding refresh runs relative to the deferred pump (for non-immediate modes), e.g. <see cref="DeferredBindingCoroutineHost"/>.
    /// </summary>
    public enum BindingUpdateTiming
    {
        /// <summary>
        /// Run getter and target update synchronously on each <see cref="IBindings.UpdateBind"/> call.
        /// </summary>
        Immediate = 0,

        /// <summary>
        /// Defer target updates; the deferred pump runs them on the next frame (see <see cref="DeferredBindingCoroutineHost"/>).
        /// </summary>
        NextFrame = 1,

        /// <summary>
        /// Defer target updates; the deferred pump runs them after frame rendering in Unity (see <see cref="DeferredBindingCoroutineHost"/>).
        /// </summary>
        EndOfFrame = 2,
    }
}
