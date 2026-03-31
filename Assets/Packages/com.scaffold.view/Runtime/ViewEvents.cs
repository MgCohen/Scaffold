using UnityEngine;
using Scaffold.Navigation.Contracts;
using Scaffold.MVVM.Binding;
using System.Collections.Generic;
using System;
using Scaffold.MVVM.Contracts;
namespace Scaffold.MVVM
{
    public static class ViewEvents
    {
        private static readonly Dictionary<Type, IEventLedger> ledgers = new();
        private static EventLedgerExceptionOptions exceptionOptions = CreateDefaultExceptionOptions();

        public static EventLedgerExceptionOptions GetExceptionOptions()
        {
            EnsureExceptionOptionsState();
            return exceptionOptions;
        }

        public static void SetExceptionOptions(EventLedgerExceptionOptions options)
        {
            if (options is null)
{
    throw new ArgumentNullException(nameof(options));
}
            exceptionOptions = options;
        }

        public static IDisposable PushExceptionOptions(EventLedgerExceptionOptions options)
        {
            if (options is null)
{
    throw new ArgumentNullException(nameof(options));
}
            return new ExceptionOptionsScope(options);
        }

        public static void Raise<TEvent>(MonoBehaviour source, TEvent evt) where TEvent : ViewEvent
        {
            if (source is null)
{
    throw new ArgumentNullException(nameof(source));
}
            if (evt is null)
{
    throw new ArgumentNullException(nameof(evt));
}
            Raise(source.transform, evt);
        }

        public static void Raise<TEvent>(Transform source, TEvent evt) where TEvent : ViewEvent
        {
            if (source is null)
{
    throw new ArgumentNullException(nameof(source));
}
            if (evt is null)
{
    throw new ArgumentNullException(nameof(evt));
}
            var ledger = GetLedger<TEvent>(false);
            ledger?.Raise(source, evt);
        }

        public static void Raise(MonoBehaviour source, ViewEvent evt)
        {
            if (source is null)
{
    throw new ArgumentNullException(nameof(source));
}
            if (evt is null)
{
    throw new ArgumentNullException(nameof(evt));
}
            var evtType = evt.GetType();
            var ledger = GetLedger(evtType, false);
            ledger?.Raise(source.transform, evt);
        }

        public static void Register<TEvent>(MonoBehaviour eventListener, Action<TEvent> callback) where TEvent : ViewEvent
        {
            if (eventListener is null)
{
    throw new ArgumentNullException(nameof(eventListener));
}
            if (callback is null)
{
    throw new ArgumentNullException(nameof(callback));
}
            var listener = eventListener.transform;
            var ledger = GetLedger<TEvent>(true);
            ledger?.Register(listener, callback);
        }

        public static void Register(Type evtType, MonoBehaviour eventListener, Action<IViewEvent> callback)
        {
            GuardRegisterInput(evtType, eventListener, callback);
            var listener = eventListener.transform;
            var ledger = GetLedger(evtType, true);
            ledger.Register(listener, callback);
        }

        public static void Unregister<TEvent>(MonoBehaviour eventListener, Action<TEvent> callback) where TEvent : ViewEvent
        {
            if (eventListener is null)
{
    throw new ArgumentNullException(nameof(eventListener));
}
            if (callback is null)
{
    throw new ArgumentNullException(nameof(callback));
}
            var listener = eventListener.transform;
            var ledger = GetLedger<TEvent>(false);
            ledger?.Unregister(listener, callback);
        }

        public static void Unregister(Type evtType, MonoBehaviour eventListener, Action<IViewEvent> callback)
        {
            GuardUnregisterInput(evtType, eventListener, callback);
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
            GuardEventType(evtType);
            if (!createIfMissing)
{
    return TryGetLedger(evtType);
}
            if (!ledgers.TryGetValue(evtType, out var ledger))
            {
                ledger = CreateLedger(evtType);
                ledgers[evtType] = ledger;
            }
            return ledger;
        }

        private static IEventLedger TryGetLedger(Type evtType)
        {
            ledgers.TryGetValue(evtType, out var ledger);
            return ledger;
        }

        private static void GuardEventType(Type evtType)
        {
            if (evtType is null)
{
    throw new ArgumentNullException(nameof(evtType));
}
        }

        private static void GuardRegisterInput(Type evtType, MonoBehaviour eventListener, Action<IViewEvent> callback)
        {
            GuardEventType(evtType);
            GuardEventListener(eventListener);
            GuardCallback(callback);
        }

        private static void GuardUnregisterInput(Type evtType, MonoBehaviour eventListener, Action<IViewEvent> callback)
        {
            GuardEventType(evtType);
            GuardEventListener(eventListener);
            GuardCallback(callback);
        }

        private static void GuardEventListener(MonoBehaviour eventListener)
        {
            if (eventListener is null)
{
    throw new ArgumentNullException(nameof(eventListener));
}
        }

        private static void GuardCallback(Action<IViewEvent> callback)
        {
            if (callback is null)
{
    throw new ArgumentNullException(nameof(callback));
}
        }

        private static IEventLedger CreateLedger(Type evtType)
        {
            var genericLedger = typeof(EventLedger<>);
            var typedLedger = genericLedger.MakeGenericType(evtType);
            return (IEventLedger)Activator.CreateInstance(typedLedger);
        }

        private static EventLedgerExceptionOptions CreateDefaultExceptionOptions()
        {
            return new EventLedgerExceptionOptions(
                EventLedgerExceptionMode.ReportAndContinue,
                (ex, eventType) =>
                {
                    Debug.LogException(ex);
                    Debug.LogError($"Error on invoking callback for {eventType}");
                });
        }

        private static void EnsureExceptionOptionsState()
        {
            if (exceptionOptions is null)
{
    throw new InvalidOperationException("Event ledger exception options were not initialized.");
}
        }

        private sealed class ExceptionOptionsScope : IDisposable
        {
            internal ExceptionOptionsScope(EventLedgerExceptionOptions options)
            {
                if (options is null)
{
    throw new ArgumentNullException(nameof(options));
}
                previousOptions = exceptionOptions;
                exceptionOptions = options;
            }

            private readonly EventLedgerExceptionOptions previousOptions;
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
{
    return;
}
                exceptionOptions = previousOptions;
                disposed = true;
            }
        }
    }
}





