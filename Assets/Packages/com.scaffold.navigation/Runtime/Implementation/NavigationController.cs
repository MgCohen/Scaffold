using UnityEngine;
using Scaffold.Types;
using Scaffold.Events.Contracts;
using Scaffold.Events;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Scaffold.Addressables.Contracts;
using Scaffold.Navigation.Contracts;
namespace Scaffold.Navigation
{
    public class NavigationController : INavigation
    {
        public NavigationController(IEventBus events, NavigationSettings settings, Transform viewHolder, IEnumerable<INavigationMiddleware> middlewares, IAddressablesGateway addressablesGateway, IViewControllerDependencyInjector dependencyInjector = null)
        {
            if (events is null)
            {
                throw new System.ArgumentNullException(nameof(events));
            }
            if (settings is null)
            {
                throw new System.ArgumentNullException(nameof(settings));
            }
            if (viewHolder is null)
            {
                throw new System.ArgumentNullException(nameof(viewHolder));
            }
            if (middlewares is null)
            {
                throw new System.ArgumentNullException(nameof(middlewares));
            }
            if (addressablesGateway is null)
            {
                throw new System.ArgumentNullException(nameof(addressablesGateway));
            }
            this.settings = settings;
            this.viewHolder = viewHolder;
            this.dependencyInjector = dependencyInjector;

            stack = new NavigationStack();
            provider = new NavigationProvider(settings, viewHolder, addressablesGateway);
            transitions = new NavigationTransitions(events);
            middleware = new NavigationMiddleware(middlewares);
        }

        public NavigationPoint CurrentPoint => this.stack.CurrentPoint;
        public IViewController CurrentController => this.stack.CurrentPoint?.ViewModel;

        private NavigationSettings settings;
        private Transform viewHolder;
        private NavigationStack stack;
        private NavigationProvider provider;
        private NavigationTransitions transitions;
        private NavigationMiddleware middleware;
        private readonly IViewControllerDependencyInjector dependencyInjector;

        public void Open<TController>(TController controller, NavigationOptions options) where TController : IViewController
        {
            Open(controller, false, options);
        }

        public void Open<TController>(TController controller, bool closeCurrent = false, NavigationOptions options = null) where TController : IViewController
        {
            if (stack == null || provider == null || transitions == null || middleware == null) throw new InvalidOperationException("NavigationController has not been initialized correctly.");
            options ??= new NavigationOptions();
            NavigationPoint point = provider.GetNavigationPoint<TController>(controller, options);
            Open(point, closeCurrent, options);
        }

        public void PrepareDependencies(IViewController controller)
        {
            if (controller is null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            dependencyInjector?.Inject(controller);
        }

        private void Open(NavigationPoint point, bool closeCurrent, NavigationOptions options)
        {
            middleware.OnOpen(point.ViewModel);
            point.ViewModel.Bind(this);
            GoTo(point, closeCurrent, options);
        }

        public void Close<TViewController>(TViewController controller) where TViewController : IViewController
        {
            if (stack == null || provider == null || transitions == null || middleware == null) throw new InvalidOperationException("NavigationController has not been initialized correctly.");
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
                RemoveAndDispose(point);
            }
        }

        public IViewController Return()
        {
            if (stack == null || provider == null || transitions == null || middleware == null) throw new InvalidOperationException("NavigationController has not been initialized correctly.");
            var targetPoint = this.stack.PreviousPoint;
            if (targetPoint == null)
            {
                return null;
            }
            var defaultOptions = new NavigationOptions();
            GoTo(targetPoint, true, defaultOptions);
            return targetPoint.ViewModel;
        }

        private void GoTo(NavigationPoint point, bool closeCurrent, NavigationOptions options)
        {
            options ??= new NavigationOptions();
            NavigationStackResolver.Resolve(options, closeCurrent, out bool closeAllBelowCurrent, out bool removeCurrentFromStack);
            NavigationPoint from = this.CurrentPoint;
            ApplyStackMutation(point, from, closeAllBelowCurrent, removeCurrentFromStack);
            CompleteTransitionToPoint(point, from, removeCurrentFromStack);
        }

        private void ApplyStackMutation(NavigationPoint point, NavigationPoint from, bool closeAllBelowCurrent, bool removeCurrentFromStack)
        {
            if (closeAllBelowCurrent)
            {
                CloseAll(from);
            }

            if (removeCurrentFromStack && this.CurrentPoint != null)
            {
                this.stack.RemoveFromStack(this.CurrentPoint);
            }

            if (this.CurrentPoint != point)
            {
                this.stack.AddToStack(point);
            }
        }

        private void CompleteTransitionToPoint(NavigationPoint point, NavigationPoint from, bool removeCurrentFromStack)
        {
            var depth = this.stack.GetPointDepth(point);
            point.SetDepth(depth, point.Options);
            this.transitions.DoTransition(from, point, removeCurrentFromStack);
        }

        private void CloseAll(NavigationPoint point)
        {
            var substack = stack.GetAllStackedScreens((p) => p != point);
            foreach (var otherPoint in substack)
            {
                RemoveAndDispose(otherPoint);
            }
        }

        private void RemoveAndDispose(NavigationPoint point)
        {
            stack.RemoveFromStack(point);
            ClosePointView(point);
            point.Dispose();
        }

        private void ClosePointView(NavigationPoint point)
        {
            if (point?.View != null)
            {
                point.View.Close();
            }
        }
    }
}






