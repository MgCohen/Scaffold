using System;
using System.Collections.Generic;
using Scaffold.Addressables.Contracts;
using Scaffold.Navigation.Contracts;
using UnityEngine;

namespace Scaffold.Navigation
{
    internal class NavigationProvider
    {
        public NavigationProvider(NavigationSettings settings, Transform viewHolder, IAddressablesGateway addressables)
        {
            GuardConstructor(settings, viewHolder, addressables);
            this.settings = settings;
            this.viewHolder = viewHolder;
            assetHandleBuffer = new NavigationAssetHandleBuffer();
            FetchContextViews();
            pointStrategies = new List<INavigationPointStrategy>
            {
                new ContextNavigationPointStrategy(contextViews),
                new AddressablesNavigationPointStrategy(addressables, viewHolder, assetHandleBuffer)
            };
        }

        private readonly Transform viewHolder;
        private readonly NavigationSettings settings;
        private readonly NavigationAssetHandleBuffer assetHandleBuffer;
        private readonly Dictionary<Type, IView> contextViews = new Dictionary<Type, IView>();
        private readonly List<INavigationPointStrategy> pointStrategies;

        public NavigationPoint GetNavigationPoint<TController>(TController controller, NavigationOptions options) where TController : IViewController
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            ViewConfig config = settings.GetViewConfig(typeof(TController));
            NavigationOptions resolved = options;
            if (resolved == null && config.TryGetSchema<NavigationOptionsSchema>(out NavigationOptionsSchema schema)) resolved = schema.Options;
            resolved ??= new NavigationOptions();
            for (int i = 0; i < pointStrategies.Count; i++)
            {
                if (pointStrategies[i].TryCreate(config, controller, resolved, out NavigationPoint point)) return point;
            }

            throw new InvalidOperationException("No navigation point source could resolve the requested view.");
        }

        private void FetchContextViews()
        {
            IView[] views = viewHolder.GetComponentsInChildren<IView>(true);
            foreach (IView view in views)
            {
                contextViews[view.GetType()] = view;
                view.gameObject.SetActive(false);
            }
        }

        private void GuardConstructor(NavigationSettings settings, Transform viewHolder, IAddressablesGateway addressables)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (viewHolder == null) throw new ArgumentNullException(nameof(viewHolder));
            if (addressables == null) throw new ArgumentNullException(nameof(addressables));
        }
    }
}
