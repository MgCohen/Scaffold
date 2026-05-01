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
                new DirectPrefabNavigationPointStrategy(viewHolder),
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

        private sealed class DirectPrefabNavigationPointStrategy : INavigationPointStrategy
        {
            public DirectPrefabNavigationPointStrategy(Transform viewHolder)
            {
                this.viewHolder = viewHolder;
            }

            private readonly Transform viewHolder;

            public bool TryCreate(ViewConfig config, IViewController controller, NavigationOptions options, out NavigationPoint point)
            {
                if (!CanCreateDirectPrefab(config))
                {
                    point = null;
                    return false;
                }

                point = new NavigationPoint(controller, config, false, options, DisposeNavigationPoint);
                CompletePointWithInstance(point, config.DirectPrefab);
                return true;
            }

            private bool CanCreateDirectPrefab(ViewConfig config)
            {
                return config != null
                    && config.AssetSource == ViewAssetSource.DirectPrefab
                    && config.DirectPrefab != null;
            }

            private void CompletePointWithInstance(NavigationPoint point, GameObject prefab)
            {
                if (point == null || point.Disposed || prefab == null)
                {
                    return;
                }

                GameObject instance = GameObject.Instantiate(prefab, viewHolder);
                IView view = instance.GetComponent<IView>();
                if (view == null)
                {
                    DestroyInvalidViewInstance(instance);
                    throw new InvalidOperationException($"Direct-prefab view '{instance.name}' does not implement {nameof(IView)}.");
                }

                instance.SetActive(false);
                point.CompleteReady(view);
            }

            private void DestroyInvalidViewInstance(GameObject instance)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(instance);
                    return;
                }

                UnityEngine.Object.DestroyImmediate(instance);
            }

            private void DisposeNavigationPoint(NavigationPoint disposed)
            {
                if (disposed == null || disposed.IsSceneView || disposed.View == null)
                {
                    return;
                }

                DestroyViewObject(disposed.View.gameObject);
            }

            private void DestroyViewObject(UnityEngine.Object instance)
            {
                if (instance == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(instance);
                    return;
                }

                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
