using UnityEngine;
using Scaffold.Types;
using Scaffold.Events.Contracts;
using Scaffold.Events;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Scaffold.Navigation.Contracts;
namespace Scaffold.Navigation
{
    public class NavigationMiddleware
    {
        public NavigationMiddleware(IEnumerable<INavigationMiddleware> middlewares)
        {
            if (middlewares is null)
            {
                throw new System.ArgumentNullException(nameof(middlewares));
            }
            openHandlers = middlewares.OfType<INavigationOpenHandler>();
        }

        private IEnumerable<INavigationOpenHandler> openHandlers;

        public void OnOpen(IViewController viewModel)
        {
            GuardHandlers();
            foreach (var openHandler in openHandlers)
{
    openHandler.OnOpen(viewModel);
}
        }

        private void GuardHandlers()
        {
            if (openHandlers == null)
            {
                throw new System.InvalidOperationException("Open handlers are not initialized.");
            }
        }
    }
}





