using VContainer.Unity;
using VContainer;
using UnityEngine;
using Scaffold.Navigation.Contracts;
using Scaffold.Scope.Contracts;

namespace Scaffold.Navigation.Container
{
    internal class NavigationInjection : INavigationOpenHandler
    {
        public NavigationInjection(ICrossLayerObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        private readonly ICrossLayerObjectResolver resolver;

        public void OnOpen(IViewController viewModel)
        {
            resolver.Inject(viewModel);
        }
    }
}



