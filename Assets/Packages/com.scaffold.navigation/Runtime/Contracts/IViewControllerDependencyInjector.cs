namespace Scaffold.Navigation.Contracts
{
    public interface IViewControllerDependencyInjector
    {
        void Inject(IViewController controller);
    }
}
