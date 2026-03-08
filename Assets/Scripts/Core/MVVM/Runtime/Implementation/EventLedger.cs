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
            var callbacks = GetCallbackList(source, true);
            callbacks.typed.Add(evt);
        }

        public void Register(Transform source, Action<ViewEvent> evt)
        {
            var callbacks = GetCallbackList(source, true);
            callbacks.generic.Add(evt);
        }

        public void Unregister(Transform source, Action<T> evt)
        {
            var callbacks = GetCallbackList(source, false);
            callbacks.typed?.Remove(evt);
        }

        public void Unregister(Transform source, Action<ViewEvent> evt)
        {
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
            while (transform != null && !evt.IsConsumed)
            {
                RaiseAtTransform(transform, evt);
                transform = transform.parent;
            }
        }

        private void RaiseAtTransform(Transform transform, T evt)
        {
            evt.LogNext(transform);
            var callbacks = GetCallbackList(transform, false);
            TryRaiseCallbackList(callbacks.typed, evt);
            TryRaiseCallbackList(callbacks.generic, evt);
        }

        private void TryRaiseCallbackList<T1>(List<Action<T1>> list, T1 evt) where T1 : ViewEvent
        {
            if (list == null) { return; }
            RaiseListCallbacks(list, evt);
        }

        private void RaiseListCallbacks<T1>(List<Action<T1>> list, T1 evt) where T1 : ViewEvent
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (evt.IsConsumed) { return; }
                InvokeCallback(list[i], evt);
            }
        }

        private void InvokeCallback<T1>(Action<T1> action, T1 evt) where T1 : ViewEvent
        {
            try { action?.Invoke(evt); }
            catch (Exception ex) { LogCallbackError(ex); }
        }

        private void LogCallbackError(Exception ex)
        {
            Debug.LogException(ex);
            Debug.LogError($"Error on invoking callback for {typeof(T)}");
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
