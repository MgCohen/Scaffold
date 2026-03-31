namespace Scaffold.Navigation.Contracts
{
    public interface INavigationOpenHandler : INavigationMiddleware
    {
        void OnOpen(IViewController viewModel);
    }
}




