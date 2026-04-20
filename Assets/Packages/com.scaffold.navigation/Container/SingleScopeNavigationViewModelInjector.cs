using Scaffold.Navigation.Contracts;
using VContainer;

namespace Scaffold.Navigation.Container
{
    internal sealed class SingleScopeNavigationViewModelInjector : INavigationViewModelInjector
    {
        private readonly IObjectResolver owningScope;

        public SingleScopeNavigationViewModelInjector(IObjectResolver owningScope)
        {
            this.owningScope = owningScope;
        }

        public void Inject(IViewController viewModel)
        {
            owningScope.Inject(viewModel);
        }
    }
}
