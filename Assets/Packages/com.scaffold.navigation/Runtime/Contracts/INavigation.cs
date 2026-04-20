namespace Scaffold.Navigation.Contracts
{
    public interface INavigation
    {
        void Open<TViewController>(TViewController controller, NavigationOptions options) where TViewController : IViewController;

        void Open<TViewController>(TViewController controller, bool closeCurrent = false, NavigationOptions options = null) where TViewController : IViewController;

        void PrepareDependencies(IViewController controller);

        void Close<TViewController>(TViewController controller) where TViewController : IViewController;

        IViewController Return();

        IViewController CurrentController { get; }
    }
}
