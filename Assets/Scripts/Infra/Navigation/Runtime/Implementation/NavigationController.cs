using Scaffold.Events;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Navigation
{
    public class NavigationController : INavigation
    {
        private NavigationSettings settings;
        private Transform viewHolder;

        private NavigationStack stack;
        private NavigationProvider provider;
        private NavigationTransitions transitions;

        private NavigationMiddleware middleware;

        public NavigationPoint CurrentPoint => this.stack.CurrentPoint;

        public NavigationController(IEventBus events, NavigationSettings settings, Transform viewHolder, IEnumerable<INavigationMiddleware> middlewares)
        {
            this.settings = settings;
            this.viewHolder = viewHolder;

            stack = new NavigationStack();
            provider = new NavigationProvider(settings, viewHolder);
            transitions = new NavigationTransitions(events);
            middleware = new NavigationMiddleware(middlewares);
        }

        public void Open<TController>(TController controller, bool closeCurrent = false, NavigationOptions options = null) where TController : IViewController
        {
            options ??= new NavigationOptions();
            NavigationPoint point = provider.GetNavigationPoint<TController>(controller, options);
            Open(point, closeCurrent, options);
        }

        private void Open(NavigationPoint point, bool closeCurrent, NavigationOptions options)
        {
            middleware.OnOpen(point.ViewModel);
            point.ViewModel.Bind(this);
            GoTo(point, closeCurrent, options);
        }

        public void Close<TViewController>(TViewController controller) where TViewController : IViewController
        {
            var point = this.stack.Get(controller);
            if (point == null)
            {
                Debug.LogWarning("Trying to close a view that is no longer in the stack");
                return;
            }
            ClosePoint(point);
        }

        private void ClosePoint(NavigationPoint point)
        {
            if (point == CurrentPoint)
            {
                Return();
            }
            else
            {
                ForceRemovePoint(point);
            }
        }

        private void ForceRemovePoint(NavigationPoint point)
        {
            this.stack.RemoveFromStack(point);
            ForceClosePoint(point);
        }

        public IViewController Return()
        {
            var targetPoint = this.stack.PreviousPoint;
            var defaultOptions = new NavigationOptions();
            GoTo(targetPoint, true, defaultOptions);
            return targetPoint.ViewModel;
        }

        private void ForceClosePoint(NavigationPoint point)
        {
            point.View.Close();
            if (!point.IsSceneView)
            {
                GameObject.Destroy(point.View.gameObject);
                point.Dispose();
            }
        }

        private void GoTo(NavigationPoint point, bool closeCurrent, NavigationOptions options)
        {
            var from = this.CurrentPoint;
            if (options.CloseAllViews.HasValue && options.CloseAllViews.Value)
            {
                CloseAll(from);
            }
            UpdateStack(point, closeCurrent);
            ActivatePoint(from, point, closeCurrent);
        }

        private void UpdateStack(NavigationPoint point, bool closeCurrent)
        {
            if (closeCurrent && this.CurrentPoint != null)
            {
                this.stack.RemoveFromStack(this.CurrentPoint);
            }
            if (this.CurrentPoint != point)
            {
                this.stack.AddToStack(point);
            }
        }

        private void ActivatePoint(NavigationPoint from, NavigationPoint point, bool closeCurrent)
        {
            var depth = this.stack.GetPointDepth(point);
            point.SetDepth(depth, point.Options);
            this.transitions.DoTransition(from, point, closeCurrent);
        }

        private void CloseAll(NavigationPoint point)
        {
            var substack = stack.GetAllStackedScreens((p) => p != point);
            foreach (var oPoint in substack)
            {
                stack.RemoveFromStack(oPoint);
                oPoint.View.Close();
            }
        }
    }
}
