namespace Scaffold.MVVM.Binding
{
    /// <summary>
    /// Implemented by <see cref="TreeBinding"/>; used by <see cref="BindContext{T}"/> to queue deferred flushes.
    /// </summary>
    public interface IBindingDeferredCoordinator
    {
        bool IsUnbinding { get; }

        void RequestDeferredFlush(IBindContext context);
    }
}
