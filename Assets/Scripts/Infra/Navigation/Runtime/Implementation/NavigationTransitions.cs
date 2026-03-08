using Scaffold.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scaffold.Navigation
{
    internal class NavigationTransitions
    {
        public NavigationTransitions(IEventBus events)
        {
            this.events = events;
        }

        private IEventBus events;
        private bool runningTransition = false;
        private Queue<ViewTransitionData> pendingTransitions = new Queue<ViewTransitionData>();

        public Action<ViewTransitionData> TransitionFinished = delegate { };

        public void DoTransition(NavigationPoint from, NavigationPoint to, bool closeCurrent)
        {
            var transitionData = new ViewTransitionData(from, to, closeCurrent);
            pendingTransitions.Enqueue(transitionData);
            if (!runningTransition)
            {
                RunTransitions();
            }
        }

        private async void RunTransitions()
        {
            runningTransition = true;
            while (pendingTransitions.Count > 0)
            {
                var transition = pendingTransitions.Dequeue();
                if (transition != null)
                {
                    transition.OpenningSequence = () => DoOpenSequence(transition.From, transition.To);
                    transition.ClosingSequence = () => DoCloseSequence(transition.From, transition.To);
                    transition.HidingSequence = () => DoHideSequence(transition.From, transition.To);
                    var transitionSchema = GetTransitionSchema(transition, out var direction);
                    if (transitionSchema != null)
                    {
                        await ResolveTransitionSchema(transitionSchema, transition, direction);
                    }
                    else
                    {
                        await DefaultViewTransition(transition);
                    }
                    TransitionFinished?.Invoke(transition);
                }
            }
            runningTransition = false;
        }


        #region View Transitions

        private TransitionViewSchema GetTransitionSchema(ViewTransitionData transition, out TransitionDirection direction)
        {
            if (transition?.From?.Config != null && transition.From.Config.TryGetSchema<TransitionViewSchema>(out var fromSchema))
            {
                if (fromSchema.IsValidTransition(transition.From, transition.To, TransitionDirection.FromThisView))
                {
                    direction = TransitionDirection.FromThisView;
                    return fromSchema;
                }
            }

            if (transition?.From?.Config != null && transition.To.Config.TryGetSchema<TransitionViewSchema>(out var toSchema))
            {
                if (toSchema.IsValidTransition(transition.From, transition.To, TransitionDirection.ToThisView))
                {
                    direction = TransitionDirection.ToThisView;
                    return toSchema;
                }
            }

            direction = default;
            return null;
        }

        private async Awaitable ResolveTransitionSchema(TransitionViewSchema schema, ViewTransitionData transition, TransitionDirection direction)
        {
            if (schema.Handler is TransitionHandler.Code)
            {
                await HandleCodeTransitions(transition, direction);
            }
            else if (schema.Handler is TransitionHandler.Template)
            {
                throw new Exception("No handler for template transitions was defined yet");
            }
            else if (schema.Handler is TransitionHandler.Default)
            {
                await DefaultViewTransition(transition);
            }
        }

        #endregion

        #region Transition Handlers

        private async Awaitable HandleCodeTransitions(ViewTransitionData transition, TransitionDirection direction)
        {
            var point = direction is TransitionDirection.ToThisView ? transition.To : transition.From;
            await point.View.gameObject.GetComponent<IViewTransitionHandler>().DoTransition(transition, direction);
        }

        private async Awaitable DefaultViewTransition(ViewTransitionData transition)
        {
            if (transition.From != null)
            {
                if (transition.CloseCurrent)
                {
                    await transition.ClosingSequence();
                }
                else
                {
                    await transition.HidingSequence();
                }
            }

            if (transition.To != null)
            {
                await transition.OpenningSequence();
            }
        }
        #endregion

        #region View Animations

        private AnimationViewSchema GetAnimationSchema(NavigationPoint from, NavigationPoint to, AnimationType direction)
        {
            var config = direction is AnimationType.Opening ? to.Config : from.Config;
            var schemas = config.GetSchemas<AnimationViewSchema>();
            return schemas.FirstOrDefault(s => s.IsValidAnimation(from, to, direction));
        }

        private async Awaitable ResolveAnimationSchema(AnimationViewSchema animationSchema, NavigationPoint point, AnimationType direction)
        {
            if (animationSchema.Handler is AnimationHandler.Animator)
            {
                await this.HandleAnimator(animationSchema, point);
            }
            else if (animationSchema.Handler is AnimationHandler.Code)
            {
                await this.HandleCodeAnimation(point, direction);
            }
            else if (animationSchema.Handler is AnimationHandler.Template)
            {
                throw new System.Exception("No handler for template animations was defined yet");
            }
        }

        private async Awaitable DoCloseSequence(NavigationPoint from, NavigationPoint to)
        {
            var viewType = from.ViewModel.GetType();
            var beforeCloseEvent = new BeforeViewCloseEvent(viewType);
            events.Raise(beforeCloseEvent);
            var transitionSchema = this.GetAnimationSchema(from, to, AnimationType.Closing);
            if (transitionSchema != null)
            {
                await this.ResolveAnimationSchema(transitionSchema, from, AnimationType.Closing);
            }
            this.Close(from);
            var afterCloseEvent = new AfterViewCloseEvent(viewType);
            events.Raise(afterCloseEvent);
        }

        private async Awaitable DoHideSequence(NavigationPoint from, NavigationPoint to)
        {
            var viewType = from.ViewModel.GetType();
            var beforeHideEvent = new BeforeViewCloseEvent(viewType);
            events.Raise(beforeHideEvent);
            var transitionSchema = this.GetAnimationSchema(from, to, AnimationType.Hiding);
            if (transitionSchema != null)
            {
                await this.ResolveAnimationSchema(transitionSchema, from, AnimationType.Hiding);
            }
            this.Hide(from, to);
            var afterHideEvent = new AfterViewCloseEvent(viewType);
            events.Raise(afterHideEvent);
        }

        private void Close(NavigationPoint point)
        {
            if (point.View == null || point.View.gameObject == null)
            {
                return;
            }

            point.View.Close();
            if (!point.IsSceneView)
            {
                GameObject.Destroy(point.View.gameObject);
            }
        }

        private void Hide(NavigationPoint point, NavigationPoint to)
        {
            if (point.View == null || point.View.gameObject == null)
            {
                return;
            }

            if (to.View == null || to.View.gameObject == null)
            {
                point.View.Hide();
                return;
            }

            if (to.View.Type is ViewType.Screen)
            {
                point.View.Hide();
            }
        }

        private async Awaitable DoOpenSequence(NavigationPoint from, NavigationPoint to)
        {
            var viewType = to.ViewModel.GetType();
            var beforeOpenEvent = new BeforeViewOpenEvent(viewType);
            events.Raise(beforeOpenEvent);
            to.View.gameObject.SetActive(true);
            if (to.View.State is ViewState.Closed)
            {
                to.View.Bind(to.ViewModel);
            }
            var transitionSchema = this.GetAnimationSchema(from, to, AnimationType.Opening);
            if (transitionSchema != null)
            {
                await this.ResolveAnimationSchema(transitionSchema, to, AnimationType.Opening);
            }
            this.Open(to);
            var afterOpenEvent = new AfterViewOpenEvent(viewType);
            events.Raise(afterOpenEvent);
        }

        private void Open(NavigationPoint point)
        {
            try
            {
                if (point?.Disposed == true || point?.View == null || point?.View?.gameObject == null)
                {
                    return;
                }

                if (point.View.State is ViewState.Open)
                {
                    point.View.Focus();
                }
                else
                {
                    point.View.Open();
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Catched a problem while opening a point");
                Debug.LogException(ex);
                return;
            }
        }

        #endregion

        #region Animation Handlers
        private async Awaitable HandleAnimator(AnimationViewSchema schema, NavigationPoint point)
        {
            await Awaitable.WaitForSecondsAsync(0.02f);

            var animator = point.View.gameObject.GetComponent<Animator>();
            animator.Play(schema.AnimationName);

            var stateHash = Animator.StringToHash(schema.AnimationName);
            if (!animator.HasState(0, stateHash))
            {
                throw new System.Exception($"No valid state {schema.AnimationName} in this animator");
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            while (!state.IsName(schema.AnimationName))
            {
                await Awaitable.WaitForSecondsAsync(0.1f);
                state = animator.GetCurrentAnimatorStateInfo(0);
            }

            while (state.IsName(schema.AnimationName) && state.normalizedTime <= 1)
            {
                await Awaitable.WaitForSecondsAsync(0.1f);
                state = animator.GetCurrentAnimatorStateInfo(0);
            }

            while (state.normalizedTime <= 1)
            {
                await Awaitable.WaitForSecondsAsync(0.1f);
                state = animator.GetCurrentAnimatorStateInfo(0);
            }
        }


        private async Awaitable HandleCodeAnimation(NavigationPoint point, AnimationType direction)
        {
            await point.View.gameObject.GetComponent<IViewAnimationHandler>().AnimateView(direction);
        }
        #endregion
    }
}
