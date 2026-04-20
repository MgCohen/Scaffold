using Scaffold.LayeredScope;
using Scaffold.Navigation.Contracts;

namespace Scaffold.Navigation.Container
{
    internal class NavigationInjection : INavigationOpenHandler
    {
        public NavigationInjection(ILayerResolver layers)
        {
            this.layers = layers;
        }

        private readonly ILayerResolver layers;

        public void OnOpen(IViewController viewModel)
        {
            layers.Top.Inject(viewModel);
        }
    }
}

