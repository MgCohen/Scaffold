namespace Scaffold.Navigation
{
    public static class NavigationExtensions
    {
        public static TViewController Open<TViewController>(this INavigation navigation, TViewController controller = default, bool closeOpenedWindow = false) where TViewController : IViewController, new()
        {
            controller ??= new TViewController();
            navigation.Open(controller, closeOpenedWindow);
            return controller;
        }
    }
}