using Scaffold.Navigation.Contracts;

namespace Scaffold.Navigation
{
    internal interface INavigationPointStrategy
    {
        bool TryCreate(ViewConfig config, IViewController controller, NavigationOptions options, out NavigationPoint point);
    }
}
