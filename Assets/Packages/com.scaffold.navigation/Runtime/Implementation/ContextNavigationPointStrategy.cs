using System;
using System.Collections.Generic;
using Scaffold.Navigation.Contracts;

namespace Scaffold.Navigation
{
    internal sealed class ContextNavigationPointStrategy : INavigationPointStrategy
    {
        public ContextNavigationPointStrategy(Dictionary<Type, IView> contextViews)
        {
            this.contextViews = contextViews;
        }

        private readonly Dictionary<Type, IView> contextViews;

        public bool TryCreate(ViewConfig config, IViewController controller, NavigationOptions options, out NavigationPoint point)
        {
            if (config.ViewType != null && contextViews.TryGetValue(config.ViewType, out IView contextView))
            {
                point = new NavigationPoint(contextView, controller, config, true, options);
                return true;
            }

            point = null;
            return false;
        }
    }
}
