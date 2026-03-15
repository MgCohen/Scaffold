using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.MVVM
{
    public class EventLedger<T> : IEventLedger where T : ViewEvent
    {
        private readonly Dictionary<Transform, List<Action<T>>> callbackList = new();
        private readonly Dictionary<Transform, List<Action<ViewEvent>>> genericCallbackList = new();

        public void Register(Transform source, Action<T> evt)
        {
            if (source is null) { throw new ArgumentNullException(nameof(source)); }
            if (evt is null) { throw new ArgumentNullException(nameof(evt)); }
            var callbacks = GetCallbackList(source, true);
            callbacks.typed.Add(evt);
        }

        public void Register(Transform source, Action<ViewEvent> evt)
        {
            if (source is null) { throw new ArgumentNullException(nameof(source)); }
            if (evt is null) { throw new ArgumentNullException(nameof(evt)); }
            var callbacks = GetCallbackList(source, true);
            callbacks.generic.Add(evt);
        }

        public void Unregister(Transform source, Action<T> evt)
        {
            if (source is null) { throw new ArgumentNullException(nameof(source)); }
            if (evt is null) { throw new ArgumentNullException(nameof(evt)); }
            var callbacks = GetCallbackList(source, false);
            callbacks.typed?.Remove(evt);
        }

        public void Unregister(Transform source, Action<ViewEvent> evt)
        {
            if (source is null) { throw new ArgumentNullException(nameof(source)); }
            if (evt is null) { throw new ArgumentNullException(nameof(evt)); }
            var callbacks = GetCallbackList(source, false);
            callbacks.generic?.Remove(evt);
        }

        void IEventLedger.Raise(Transform transform, ViewEvent evt)
        {
            if (evt is not T tEvt)
            {
                throw new Exception($"Trying to raise event of wrong type, tried to raise {evt.GetType()} instead of {typeof(T)}");
            }
            Raise(transform, tEvt);
        }

        public void Raise(Transform transform, T evt)
        {
            ValidateRaiseInput(transform, evt);
            List<Exception> callbackExceptions = DispatchCallbacks(transform, evt);
            HandleDispatchExceptions(callbackExceptions);
        }

        private void ValidateRaiseInput(Transform transform, T evt)
        {
            if (transform is null) { throw new ArgumentNullException(nameof(transform)); }
            if (evt is null) { throw new ArgumentNullException(nameof(evt)); }
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

        private void HandleDispatchExceptions(List<Exception> callbackExceptions)
        {
            if (callbackExceptions == null || callbackExceptions.Count == 0) { return; }
            if (ViewEvents.GetExceptionOptions().Mode != EventLedgerExceptionMode.ThrowAfterDispatch) { return; }
            throw new AggregateException("One or more callbacks failed during dispatch.", callbackExceptions);
        }

        private void RaiseAtTransform(Transform transform, T evt, ref List<Exception> callbackExceptions)
        {
            evt.LogNext(transform);
            var callbacks = GetCallbackList(transform, false);
            TryRaiseCallbackList(callbacks.typed, evt, ref callbackExceptions);
            TryRaiseCallbackList(callbacks.generic, evt, ref callbackExceptions);
        }

        private void TryRaiseCallbackList<T1>(List<Action<T1>> list, T1 evt, ref List<Exception> callbackExceptions) where T1 : ViewEvent
        {
            if (list == null) { return; }
            RaiseListCallbacks(list, evt, ref callbackExceptions);
        }

        private void RaiseListCallbacks<T1>(List<Action<T1>> list, T1 evt, ref List<Exception> callbackExceptions) where T1 : ViewEvent
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (evt.IsConsumed) { return; }
                InvokeCallback(list[i], evt, ref callbackExceptions);
            }
        }

        private void InvokeCallback<T1>(Action<T1> action, T1 evt, ref List<Exception> callbackExceptions) where T1 : ViewEvent
        {
            try { action?.Invoke(evt); }
            catch (Exception ex)
            {
                callbackExceptions ??= new List<Exception>();
                callbackExceptions.Add(ex);
                ReportCallbackException(ex);
            }
        }

        private void ReportCallbackException(Exception ex)
        {
            var options = ViewEvents.GetExceptionOptions();
            try { options.Reporter?.Invoke(ex, typeof(T)); }
            catch { }
        }

        private (List<Action<T>> typed, List<Action<ViewEvent>> generic) GetCallbackList(Transform transform, bool createIfMissing)
        {
            var typed = GetOrCreateList(callbackList, transform, createIfMissing);
            var generic = GetOrCreateList(genericCallbackList, transform, createIfMissing);
            return (typed, generic);
        }

        private List<Action<TAction>> GetOrCreateList<TAction>(Dictionary<Transform, List<Action<TAction>>> dict, Transform key, bool createIfMissing)
        {
            if (!dict.TryGetValue(key, out var list) && createIfMissing)
            {
                list = new List<Action<TAction>>();
                dict[key] = list;
            }
            return list;
        }
    }
}

