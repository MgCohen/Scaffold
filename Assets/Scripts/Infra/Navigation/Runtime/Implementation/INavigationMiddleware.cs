namespace Scaffold.Navigation
{
    public interface INavigationMiddleware
    {

    }


    public interface INavigationOpenHandler : INavigationMiddleware
    {
        void OnOpen(IViewController viewModel);
    }
}
