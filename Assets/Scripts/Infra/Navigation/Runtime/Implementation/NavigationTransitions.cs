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
    internal class NavigationTransitions
    {
        public NavigationTransitions(IEventBus events)
        {
            if (events is null)
{
    throw new ArgumentNullException(nameof(events));
}
            this.events = events;
        }

        private IEventBus events;
        private bool runningTransition = false;
        private Queue<ViewTransitionData> pendingTransitions = new Queue<ViewTransitionData>();

        public Action<ViewTransitionData> TransitionFinished = delegate { };

        public void DoTransition(NavigationPoint from, NavigationPoint to, bool closeCurrent)
        {
            if (from == null && to == null) throw new ArgumentException("At least one navigation point is required.");
            if (pendingTransitions == null) throw new InvalidOperationException("Pending transition queue is not initialized.");
            var transitionData = new ViewTransitionData(from, to, closeCurrent);
            pendingTransitions.Enqueue(transitionData);
            if (!runningTransition) RunTransitions();
        }

        private async void RunTransitions()
        {
            runningTransition = true;
            while (pendingTransitions.Count > 0)
{
    await ProcessNextTransition();
}
            runningTransition = false;
        }

        private async Task ProcessNextTransition()
        {
            var transition = pendingTransitions.Dequeue();
            if (transition != null)
            {
                if (transition.To != null) await transition.To.AwaitReadyAsync();
                await ExecuteTransition(transition);
                TransitionFinished?.Invoke(transition);
            }
        }

        private Task ExecuteTransition(ViewTransitionData transition)
        {
            if (transition?.From?.Config != null && transition.From.Config.TryGetSchema(out TransitionViewSchema fromSchema) && fromSchema.IsValidTransition(transition.From, transition.To, TransitionDirection.FromThisView)) return ExecuteResolvedTransition(fromSchema, transition, TransitionDirection.FromThisView);
            if (transition?.To?.Config != null && transition.To.Config.TryGetSchema(out TransitionViewSchema toSchema) && toSchema.IsValidTransition(transition.From, transition.To, TransitionDirection.ToThisView)) return ExecuteResolvedTransition(toSchema, transition, TransitionDirection.ToThisView);
            return ExecuteDefaultTransition(transition);
        }

        private async Task ExecuteResolvedTransition(TransitionViewSchema schema, ViewTransitionData transition, TransitionDirection direction)
        {
            if (schema.Handler is TransitionHandler.Default)
            {
                await ExecuteDefaultTransition(transition);
                return;
            }
            if (schema.Handler is TransitionHandler.Template) throw new Exception("No handler for template transitions was defined yet");
            if (schema.Handler is not TransitionHandler.Code) return;
            NavigationPoint point = direction is TransitionDirection.ToThisView ? transition.To : transition.From;
            await point.View.gameObject.GetComponent<IViewTransitionHandler>().DoTransition(transition, direction);
        }

        private async Task ExecuteDefaultTransition(ViewTransitionData transition)
        {
            await ExecuteDefaultTransitionCore(transition);
        }

        private async Task ExecuteDefaultTransitionCore(ViewTransitionData transition)
        {
            if (transition.From != null) HandleFromDefaultCore(transition);
            if (transition.To == null) return;
            var to = transition.To; var viewType = to.ViewModel.GetType();
            var beforeOpenEvent = new BeforeViewOpenEvent(viewType); events.Raise(beforeOpenEvent); to.View.gameObject.SetActive(true);
            if (to.View.State is ViewState.Closed) to.View.Bind(to.ViewModel);
            if (to?.Disposed != true && to?.View != null && to.View.gameObject != null)
            {
                if (to.View.State is ViewState.Open) to.View.Focus();
                if (to.View.State is not ViewState.Open) to.View.Open();
            }
            var afterOpenEvent = new AfterViewOpenEvent(viewType); events.Raise(afterOpenEvent);
            await Task.CompletedTask;
        }

        private void HandleFromDefaultCore(ViewTransitionData transition)
        {
            var viewType = transition.From.ViewModel.GetType(); var beforeCloseEvent = new BeforeViewCloseEvent(viewType); events.Raise(beforeCloseEvent);
            ApplyFromStateCore(transition);
            var afterCloseEvent = new AfterViewCloseEvent(viewType); events.Raise(afterCloseEvent);
        }

        private void ApplyFromStateCore(ViewTransitionData transition)
        {
            if (transition.From.View == null || transition.From.View.gameObject == null) return;
            if (transition.CloseCurrent) transition.From.View.Close();
            if (transition.CloseCurrent) transition.From.Dispose();
            if (transition.CloseCurrent) return;
            bool shouldHide = transition.To.View == null || transition.To.View.gameObject == null || transition.To.View.Type is ViewType.Screen;
            if (shouldHide) transition.From.View.Hide();
        }

    }
}






