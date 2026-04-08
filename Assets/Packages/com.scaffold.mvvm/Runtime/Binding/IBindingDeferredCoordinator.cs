namespace Scaffold.MVVM.Binding
{
    public interface IBindingDeferredCoordinator
    {
        bool IsUnbinding { get; }

        void RequestDeferredFlush(IBindContext context);
    }
}
