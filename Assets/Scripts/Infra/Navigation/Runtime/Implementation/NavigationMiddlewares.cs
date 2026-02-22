using System.Collections.Generic;
using System.Linq;

namespace Scaffold.Navigation
{
    public class NavigationMiddleware
    {
        public NavigationMiddleware(IEnumerable<INavigationMiddleware> middlewares)
        {
            openHandlers = middlewares.OfType<INavigationOpenHandler>();
        }

        private IEnumerable<INavigationOpenHandler> openHandlers;

        public void OnOpen(IViewController viewModel)
        {
            foreach(var openHandler in openHandlers)
            {
                openHandler.OnOpen(viewModel);
            }
        }
    }
}
