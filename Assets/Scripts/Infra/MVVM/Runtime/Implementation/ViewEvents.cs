using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.MVVM
{
    public class ViewEvents
    {
    private static Dictionary<Type, IEventLedger> ledgers = new Dictionary<Type, IEventLedger>();

    public static void Raise<TEvent>(Transform source, TEvent evt) where TEvent : ViewEvent
    {
        var ledger = GetLedger<TEvent>(false);
        ledger?.Raise(source, evt);
    }
    public static void Raise<TEvent>(MonoBehaviour source, TEvent evt) where TEvent : ViewEvent
    {
        Raise(source.transform, evt);
    }

    public static void Raise(MonoBehaviour source, ViewEvent evt)
    {
        var ledger = GetLedger(evt.GetType(), false);
        ledger?.Raise(source.transform, evt);
    }

    public static void Register<TEvent>(MonoBehaviour eventListener, Action<TEvent> callback) where TEvent : ViewEvent
    {
        var listener = eventListener.transform;
        var ledger = GetLedger<TEvent>(true);
        ledger?.Register(listener, callback);
    }

    public static void Register(Type evtType, MonoBehaviour eventListener, Action<ViewEvent> callback)
    {
        var listener = eventListener.transform;
        var ledger = GetLedger(evtType, true);
        ledger.Register(listener, callback);
    }

    public static void Unregister<TEvent>(MonoBehaviour eventListener, Action<TEvent> callback) where TEvent : ViewEvent
    {
        var listener = eventListener.transform;
        var ledger = GetLedger<TEvent>(false);
        ledger?.Unregister(listener, callback);
    }

    public static void Unregister(Type evtType, MonoBehaviour eventListener, Action<ViewEvent> callback)
    {
        var listener = eventListener.transform;
        var ledger = GetLedger(evtType, false);
        ledger?.Unregister(listener, callback);
    }

    private static EventLedger<TEvent> GetLedger<TEvent>(bool createIfMissing) where TEvent : ViewEvent
    {
        Type evtType = typeof(TEvent);

        if (!ledgers.TryGetValue(evtType, out var ledger) && createIfMissing)
        {
            ledger = new EventLedger<TEvent>();
            ledgers[evtType] = ledger;
        }

        return ledger as EventLedger<TEvent>;
    }

    private static IEventLedger GetLedger(Type evtType, bool createIfMissing)
    {
        if (!ledgers.TryGetValue(evtType, out var ledger) && createIfMissing)
        {
            Type genericLedger = typeof(EventLedger<>);
            Type typedLedger = genericLedger.MakeGenericType(evtType);
            ledger = (IEventLedger)Activator.CreateInstance(typedLedger);
            ledgers[evtType] = ledger;
        }

        return ledger;
    }
    }
}
