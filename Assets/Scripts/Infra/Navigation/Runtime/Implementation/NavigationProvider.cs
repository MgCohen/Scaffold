using Scaffold.MVVM;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Navigation
{

    internal class NavigationProvider
    {
        public NavigationProvider(NavigationSettings settings, Transform viewHolder)
        {
            this.settings = settings;
            this.viewHolder = viewHolder;
            FetchContextViews();
        }

        private Transform viewHolder;
        private NavigationSettings settings;
        private Dictionary<Type, IView> contextViews = new Dictionary<Type, IView>();

        private void FetchContextViews()
        {
            var views = viewHolder.GetComponentsInChildren<IView>(true);
            foreach (var view in views)
            {
                contextViews[view.GetType()] = view;
                view.gameObject.SetActive(false);
            }
        }

        public NavigationPoint GetNavigationPoint<TController>(TController controller, NavigationOptions options) where TController : IViewController
        {
            ViewConfig config = settings.GetViewConfig(typeof(TController));
            options = ValidateNavigationOptions(options, config);
            return GetNavigationPoint(config, controller, options);
        }

        private NavigationPoint GetNavigationPoint(ViewConfig config, IViewController controller, NavigationOptions options)
        {
            if (TryGetContextView(config.ViewType, out IView view))
            {
                return new NavigationPoint(view, controller, config, true, options);
            }

            if (TryGetAssetScreen(config, out view))
            {
                return new NavigationPoint(view, controller, config, false, options);
            }
            return null;
        }

        private NavigationOptions ValidateNavigationOptions(NavigationOptions options, ViewConfig config)
        {
            if (options == null)
            {
                if (config.TryGetSchema<NavigationOptionsSchema>(out var schema))
                {
                    options = schema.Options;
                }
                else
                {
                    options = new NavigationOptions();
                }
            }
            return options;
        }

        private bool TryGetContextView(Type screenType, out IView screen)
        {
            if (screenType != null && contextViews.TryGetValue(screenType, out var screenInstance))
            {
                screen = screenInstance;
                return true;
            }
            screen = null;
            return false;
        }

        private bool TryGetAssetScreen(ViewConfig config, out IView screen)
        {
            screen = GameObject.Instantiate(config.ViewAsset, viewHolder).GetComponent<IView>();
            screen.gameObject.SetActive(false);
            return screen != null;
        }

    }
}
