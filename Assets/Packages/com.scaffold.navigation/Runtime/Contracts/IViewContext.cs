namespace Scaffold.Navigation.Contracts
{
    public interface IViewContext
    {
        void Register<T>(T instance) where T : class;

        bool TryResolve<T>(out T service) where T : class;
    }
}
