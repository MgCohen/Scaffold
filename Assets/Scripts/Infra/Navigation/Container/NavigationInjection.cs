using Scaffold.Containers;

namespace Scaffold.Navigation.Container
{
    internal class NavigationInjection : INavigationOpenHandler
    {
        private IContainerResolver resolver;

        public NavigationInjection(IContainerResolver resolver)
        {
            this.resolver = resolver;
        }

        public void OnOpen(IViewController viewModel)
        {
            resolver.Inject(viewModel);
        }
    }
}
