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
        (var list, var glist) = this.GetCallbackList(source, true);
        list.Add(evt);
    }

    public void Register(Transform source, Action<ViewEvent> evt)
    {
        (var list, var glist) = this.GetCallbackList(source, true);
        glist.Add(evt);
    }

    public void Unregister(Transform source, Action<T> evt)
    {
        (var list, var glist) = this.GetCallbackList(source, false);
        list?.Remove(evt);
    }

    public void Unregister(Transform source, Action<ViewEvent> evt)
    {
        (var list, var glist) = this.GetCallbackList(source, false);
        glist?.Remove(evt);
    }

    void IEventLedger.Raise(Transform transform, ViewEvent evt)
    {
        if(evt is not T tEvt)
        {
            throw new Exception($"Trying to raise event of wrong type, tried to raise {evt.GetType()} instead of {typeof(T)}");
        }

        this.Raise(transform, tEvt);
    }

    public void Raise(Transform transform, T evt)
    {
        do
        {
            if (evt.IsConsumed)
            {
                return;
            }

            evt.LogNext(transform);
            (var list, var glist) = this.GetCallbackList(transform, false);

            if (list != null)
            {
                this.TryRaiseCallbackList(list, evt);
            }

            if (glist != null)
            {
                this.TryRaiseCallbackList(glist, evt);
            }

            transform = transform.parent;
        } while (transform != null);
    }

    private void TryRaiseCallbackList<T1>(List<Action<T1>> list, T1 evt) where T1 : ViewEvent
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var action = list[i];

            if (evt.IsConsumed)
            {
                return;
            }

            try
            {
                action?.Invoke(evt);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Debug.LogError($"Error on invoking callback for {typeof(T)}");
            }
        }
    }

    private (List<Action<T>>, List<Action<ViewEvent>>) GetCallbackList(Transform transform, bool createIfMissing)
    {
        if (!this.callbackList.TryGetValue(transform, out var list) && createIfMissing)
        {
            list = new List<Action<T>>();
            this.callbackList[transform] = list;
        }

        if (!this.genericCallbackList.TryGetValue(transform, out var glist) && createIfMissing)
        {
            glist = new List<Action<ViewEvent>>();
            this.genericCallbackList[transform] = glist;
        }


        return (list, glist);
    }
    }
}
