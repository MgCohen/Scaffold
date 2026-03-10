using System;
using System.Collections.Generic;
using Scaffold.Maps;
using UnityEngine;

namespace Scaffold.Events
{
    public class ScalableEventBus : IEventBus
    {
        private const string ExactIndexerPrefix = "exact:";
        private const string HierarchyIndexerPrefix = "hierarchy:";

        private readonly object sync = new object();
        private readonly Map<Type, long, ListenerEntry> listeners = new Map<Type, long, ListenerEntry>();
        private readonly Dictionary<Delegate, GenericRegistration> genericRegistrations = new Dictionary<Delegate, GenericRegistration>();
        private readonly Dictionary<ListenerRegistrationKey, long> listenerIds = new Dictionary<ListenerRegistrationKey, long>();

        private long nextListenerId = 1;

        public void AddListener<T>(Action<T> evt) where T : ContextEvent
        {
            ArgumentNullException.ThrowIfNull(evt);

            lock (sync)
            {
                if (genericRegistrations.ContainsKey(evt))
                {
                    return;
                }

                Action<ContextEvent> adapter = e => evt((T)e);
                AddListenerInternal(typeof(T), adapter);
                genericRegistrations.Add(evt, new GenericRegistration(typeof(T), adapter));
            }
        }

        public void RemoveListener<T>(Action<T> evt) where T : ContextEvent
        {
            ArgumentNullException.ThrowIfNull(evt);

            lock (sync)
            {
                if (!genericRegistrations.TryGetValue(evt, out GenericRegistration registration))
                {
                    return;
                }

                RemoveListenerInternal(registration.EventType, registration.Listener);
                genericRegistrations.Remove(evt);
            }
        }

        public void AddListener(Type type, Action<ContextEvent> evt)
        {
            ValidateOpenTypeArguments(type, evt);

            lock (sync)
            {
                AddListenerInternal(type, evt);
            }
        }

        public void RemoveListener(Type type, Action<ContextEvent> evt)
        {
            ValidateOpenTypeArguments(type, evt);

            lock (sync)
            {
                RemoveListenerInternal(type, evt);
            }
        }

        public void Raise(ContextEvent evt)
        {
            ArgumentNullException.ThrowIfNull(evt);

            Type actualType = evt.GetType();
            List<Action<ContextEvent>> dispatch = CaptureDispatch(actualType);
            InvokeDispatch(dispatch, evt);
        }

        public void Clear()
        {
            lock (sync)
            {
                listeners.Clear();
                genericRegistrations.Clear();
                listenerIds.Clear();
                nextListenerId = 1;
            }
        }

        private void AddListenerInternal(Type type, Action<ContextEvent> evt)
        {
            ListenerRegistrationKey key = new ListenerRegistrationKey(type, evt);
            if (listenerIds.ContainsKey(key))
            {
                return;
            }

            long listenerId = nextListenerId++;
            ListenerEntry entry = new ListenerEntry(type, listenerId, evt);
            listeners.Add(type, listenerId, entry);
            listenerIds.Add(key, listenerId);
        }

        private void RemoveListenerInternal(Type type, Action<ContextEvent> evt)
        {
            ListenerRegistrationKey key = new ListenerRegistrationKey(type, evt);
            if (!listenerIds.TryGetValue(key, out long listenerId))
            {
                return;
            }

            listeners.Remove(type, listenerId);
            listenerIds.Remove(key);
        }

        private List<Action<ContextEvent>> CaptureDispatch(Type actualType)
        {
            lock (sync)
            {
                IReadOnlyCollection<ListenerEntry> exactListeners = GetExactListeners(actualType);
                IReadOnlyCollection<ListenerEntry> hierarchyListeners = GetHierarchyListeners(actualType);
                int capacity = exactListeners.Count + hierarchyListeners.Count;
                List<Action<ContextEvent>> dispatch = new List<Action<ContextEvent>>(capacity);
                AddHandlers(dispatch, exactListeners);
                AddHandlers(dispatch, hierarchyListeners);
                return dispatch;
            }
        }

        private IReadOnlyCollection<ListenerEntry> GetExactListeners(Type actualType)
        {
            string indexerName = BuildIndexerName(ExactIndexerPrefix, actualType);
            Func<Type, long, bool> predicate = (declaredType, _) => declaredType == actualType;
            return GetIndexedListeners(indexerName, predicate);
        }

        private IReadOnlyCollection<ListenerEntry> GetHierarchyListeners(Type actualType)
        {
            string indexerName = BuildIndexerName(HierarchyIndexerPrefix, actualType);
            Func<Type, long, bool> predicate = (declaredType, _) => declaredType != actualType && declaredType.IsAssignableFrom(actualType);
            return GetIndexedListeners(indexerName, predicate);
        }

        private IReadOnlyCollection<ListenerEntry> GetIndexedListeners(string indexerName, Func<Type, long, bool> predicate)
        {
            if (!listeners.TryGetIndexer(indexerName, out _))
            {
                listeners.AddIndexer(indexerName, predicate);
            }

            return listeners.GetIndexedValues(indexerName);
        }

        private static void AddHandlers(List<Action<ContextEvent>> dispatch, IReadOnlyCollection<ListenerEntry> entries)
        {
            foreach (ListenerEntry entry in entries)
            {
                dispatch.Add(entry.Listener);
            }
        }

        private static void InvokeDispatch(List<Action<ContextEvent>> dispatch, ContextEvent evt)
        {
            foreach (Action<ContextEvent> listener in dispatch)
            {
                TryInvokeListener(listener, evt);
            }
        }

        private static void TryInvokeListener(Action<ContextEvent> listener, ContextEvent evt)
        {
            try
            {
                listener.Invoke(evt);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void ValidateOpenTypeArguments(Type type, Action<ContextEvent> evt)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(evt);

            if (!typeof(ContextEvent).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Type '{type.FullName}' must inherit from {nameof(ContextEvent)}.", nameof(type));
            }
        }

        private static string BuildIndexerName(string prefix, Type type)
        {
            string name = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
            return prefix + name;
        }

        private readonly struct ListenerEntry
        {
            public ListenerEntry(Type eventType, long listenerId, Action<ContextEvent> listener)
            {
                EventType = eventType;
                ListenerId = listenerId;
                Listener = listener;
            }

            public Type EventType { get; }
            public long ListenerId { get; }
            public Action<ContextEvent> Listener { get; }
        }

        private readonly struct GenericRegistration
        {
            public GenericRegistration(Type eventType, Action<ContextEvent> listener)
            {
                EventType = eventType;
                Listener = listener;
            }

            public Type EventType { get; }
            public Action<ContextEvent> Listener { get; }
        }

        private readonly struct ListenerRegistrationKey : IEquatable<ListenerRegistrationKey>
        {
            public ListenerRegistrationKey(Type eventType, Delegate listener)
            {
                EventType = eventType;
                Listener = listener;
            }

            private Type EventType { get; }
            private Delegate Listener { get; }

            public bool Equals(ListenerRegistrationKey other)
            {
                return EventType == other.EventType && Equals(Listener, other.Listener);
            }

            public override bool Equals(object obj)
            {
                return obj is ListenerRegistrationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(EventType, Listener);
            }
        }
    }
}
