using Scaffold.AppFlow;
using Scaffold.Navigation.Contracts;

namespace Scaffold.Navigation.Container
{
    internal class NavigationInjection : INavigationOpenHandler, IViewControllerDependencyInjector
    {
        public NavigationInjection(ILayerResolver layers)
        {
            this.layers = layers ?? throw new System.ArgumentNullException(nameof(layers));
        }

        private readonly ILayerResolver layers;

        public void OnOpen(IViewController viewModel)
        {
            Inject(viewModel);
        }

        public void Inject(IViewController controller)
        {
            if (controller == null)
            {
                throw new System.ArgumentNullException(nameof(controller));
            }

            layers.Top.Inject(controller);
        }
    }
}

