namespace Scaffold.Navigation
{
    public interface INavigationOpenHandler : INavigationMiddleware
    {
        void OnOpen(IViewController viewModel);
    }
}
