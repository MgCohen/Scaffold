namespace Scaffold.MVVM.Binding
{
    public interface IBindContext
    {
        bool IsEmpty { get; }

        void OnBindingKeyChanged();

        void FlushDeferredUpdates();

        void Unbind();
    }
}
