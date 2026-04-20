using Scaffold.Navigation.Contracts;

namespace Scaffold.Navigation.Container
{
    internal class NavigationInjection : INavigationOpenHandler
    {
        public NavigationInjection(INavigationViewModelInjector viewModelInjector)
        {
            this.viewModelInjector = viewModelInjector;
        }

        private readonly INavigationViewModelInjector viewModelInjector;

        public void OnOpen(IViewController viewModel)
        {
            viewModelInjector.Inject(viewModel);
        }
    }
}



