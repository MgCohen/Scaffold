namespace Scaffold.Navigation.Contracts
{
    public interface INavigationViewModelInjector
    {
        void Inject(IViewController viewModel);
    }
}
