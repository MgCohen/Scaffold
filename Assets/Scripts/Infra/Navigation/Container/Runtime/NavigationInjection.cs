using VContainer;

namespace Scaffold.Navigation.Container
{
    public class NavigationInjection : INavigationOpenHandler
    {
        private IObjectResolver resolver;

        public NavigationInjection(IObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public void OnOpen(IViewController viewModel)
        {
            resolver.Inject(viewModel);
        }
    }
}
