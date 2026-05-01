using System;
using System.Collections.Generic;
using Scaffold.MVVM.Contracts;
using Scaffold.Navigation.Contracts;
using UnityEngine;

namespace Scaffold.MVVM
{
    public class EventLedger<T> : IEventLedger where T : ViewEvent
    {
        private readonly Dictionary<Transform, List<Action<T>>> callbackList = new();
        private readonly Dictionary<Transform, List<Action<IViewEvent>>> genericCallbackList = new();

        public void Register(Transform source, Action<T> evt)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (evt is null) throw new ArgumentNullException(nameof(evt));

            if (!callbackList.TryGetValue(source, out var typedCallbacks))
            {
                typedCallbacks = new List<Action<T>>();
                callbackList[source] = typedCallbacks;
            }

            typedCallbacks.Add(evt);
        }

        public void Register(Transform source, Action<IViewEvent> evt)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (evt is null) throw new ArgumentNullException(nameof(evt));

            if (!genericCallbackList.TryGetValue(source, out var genericCallbacks))
            {
                genericCallbacks = new List<Action<IViewEvent>>();
                genericCallbackList[source] = genericCallbacks;
            }

            genericCallbacks.Add(evt);
        }

        public void Unregister(Transform source, Action<T> evt)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (evt is null) throw new ArgumentNullException(nameof(evt));

            if (callbackList.TryGetValue(source, out var typedCallbacks))
            {
                typedCallbacks.Remove(evt);
            }
        }

        public void Unregister(Transform source, Action<IViewEvent> evt)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (evt is null) throw new ArgumentNullException(nameof(evt));

            if (genericCallbackList.TryGetValue(source, out var genericCallbacks))
            {
                genericCallbacks.Remove(evt);
            }
        }

        void IEventLedger.Raise(Transform transform, IViewEvent evt)
        {
            if (evt is not T typedEvent)
            {
                throw new Exception($"Trying to raise event of wrong type, tried to raise {evt.GetType()} instead of {typeof(T)}");
            }

            Raise(transform, typedEvent);
        }

        public void Raise(Transform transform, T evt)
        {
            if (transform is null) throw new ArgumentNullException(nameof(transform));
            if (evt is null) throw new ArgumentNullException(nameof(evt));

            List<Exception> callbackExceptions = DispatchCallbacks(transform, evt);
            if (callbackExceptions == null || callbackExceptions.Count == 0) return;
            if (ViewEvents.GetExceptionOptions().Mode != EventLedgerExceptionMode.ThrowAfterDispatch) return;

            throw new AggregateException("One or more callbacks failed during dispatch.", callbackExceptions);
        }

        private List<Exception> DispatchCallbacks(Transform transform, T evt)
        {
            List<Exception> callbackExceptions = null;

            while (transform != null && !evt.IsConsumed)
            {
                RaiseAtTransform(transform, evt, ref callbackExceptions);
                transform = transform.parent;
            }

            return callbackExceptions;
        }

        private void RaiseAtTransform(Transform transform, T evt, ref List<Exception> callbackExceptions)
        {
            evt.LogNext(transform);

            callbackList.TryGetValue(transform, out var typedCallbacks);
            genericCallbackList.TryGetValue(transform, out var genericCallbacks);

            if (typedCallbacks != null) RaiseListCallbacks(typedCallbacks, evt, ref callbackExceptions);
            if (genericCallbacks != null) RaiseListCallbacks(genericCallbacks, evt, ref callbackExceptions);
        }

        private void RaiseListCallbacks<TEvent>(List<Action<TEvent>> callbacks, TEvent evt, ref List<Exception> callbackExceptions) where TEvent : IViewEvent
        {
            for (var i = callbacks.Count - 1; i >= 0; i--)
            {
                if (evt.IsConsumed) return;
                InvokeCallback(callbacks[i], evt, ref callbackExceptions);
            }
        }

        private void InvokeCallback<TEvent>(Action<TEvent> action, TEvent evt, ref List<Exception> callbackExceptions) where TEvent : IViewEvent
        {
            try
            {
                action?.Invoke(evt);
            }
            catch (Exception exception)
            {
                callbackExceptions ??= new List<Exception>();
                callbackExceptions.Add(exception);
                ReportCallbackException(exception);
            }
        }

        private void ReportCallbackException(Exception exception)
        {
            var options = ViewEvents.GetExceptionOptions();

            try
            {
                options.Reporter?.Invoke(exception, typeof(T));
            }
            catch
            {
            }
        }
    }
}
