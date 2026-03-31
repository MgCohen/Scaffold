namespace Scaffold.Navigation.Contracts
{
    public interface INavigation
    {
        public void Open<TViewController>(TViewController controller, bool closeCurrent = false, NavigationOptions options = null) where TViewController : IViewController;
        public void Close<TViewController>(TViewController controller) where TViewController : IViewController;
        public IViewController Return();
        IViewController CurrentController { get; }
    }
}




