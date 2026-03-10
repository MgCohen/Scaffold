using System;
using System.Collections.Generic;
using System.Threading;
using Scaffold.Maps;
using UnityEngine;

namespace Scaffold.Events
{
    public class ScalableEventBus : IEventBus, IRequestBus
    {
        private const string ExactIndexerPrefix = "exact:";
        private const string HierarchyIndexerPrefix = "hierarchy:";
        private const string RequestIndexerPrefix = "request:";

        private readonly object sync = new object();
        private readonly Map<Type, long, ListenerEntry> listeners = new Map<Type, long, ListenerEntry>();
        private readonly Map<Type, long, RequestHandlerEntry> requestHandlers = new Map<Type, long, RequestHandlerEntry>();
        private readonly Dictionary<Delegate, GenericRegistration> genericRegistrations = new Dictionary<Delegate, GenericRegistration>();
        private readonly Dictionary<ListenerRegistrationKey, long> listenerIds = new Dictionary<ListenerRegistrationKey, long>();
        private readonly Dictionary<Delegate, GenericRequestRegistration> genericRequestRegistrations = new Dictionary<Delegate, GenericRequestRegistration>();
        private readonly Dictionary<RequestRegistrationKey, long> requestHandlerIds = new Dictionary<RequestRegistrationKey, long>();

        private long nextListenerId = 1;
        private long nextRequestHandlerId = 1;

        public void AddListener<T>(Action<T> evt) where T : ContextEvent
        {
            EnsureNotNull(evt, nameof(evt));

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
            EnsureNotNull(evt, nameof(evt));

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
            EnsureNotNull(evt, nameof(evt));

            Type actualType = evt.GetType();
            List<Action<ContextEvent>> dispatch = CaptureDispatch(actualType);
            InvokeDispatch(dispatch, evt);
        }

        public void Clear()
        {
            lock (sync)
            {
                listeners.Clear();
                requestHandlers.Clear();
                genericRegistrations.Clear();
                listenerIds.Clear();
                genericRequestRegistrations.Clear();
                requestHandlerIds.Clear();
                nextListenerId = 1;
                nextRequestHandlerId = 1;
            }
        }

        public void AddRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler)
            where TRequest : ContextRequest<TResponse>
        {
            EnsureNotNull(handler, nameof(handler));

            lock (sync)
            {
                if (genericRequestRegistrations.ContainsKey(handler))
                {
                    return;
                }

                Func<object, CancellationToken, Awaitable<object>> adapter = CreateGenericRequestAdapter(handler);
                AddRequestHandlerInternal(typeof(TRequest), typeof(TResponse), adapter);
                genericRequestRegistrations.Add(handler, new GenericRequestRegistration(typeof(TRequest), typeof(TResponse), adapter));
            }
        }

        public void RemoveRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler)
            where TRequest : ContextRequest<TResponse>
        {
            EnsureNotNull(handler, nameof(handler));

            lock (sync)
            {
                if (!genericRequestRegistrations.TryGetValue(handler, out GenericRequestRegistration registration))
                {
                    return;
                }

                RemoveRequestHandlerInternal(registration.RequestType, registration.ResponseType, registration.Handler);
                genericRequestRegistrations.Remove(handler);
            }
        }

        public void AddRequestHandler(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            ValidateRequestHandlerArguments(requestType, responseType, handler);

            lock (sync)
            {
                AddRequestHandlerInternal(requestType, responseType, handler);
            }
        }

        public void RemoveRequestHandler(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            ValidateRequestHandlerArguments(requestType, responseType, handler);

            lock (sync)
            {
                RemoveRequestHandlerInternal(requestType, responseType, handler);
            }
        }

        public async Awaitable<TResponse> RequestAsync<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            EnsureNotNull(request, nameof(request));
            cancellationToken.ThrowIfCancellationRequested();

            Type requestType = request.GetType();
            RequestHandlerEntry handler = ResolveRequestHandler(requestType, typeof(TResponse));

            try
            {
                object response = await handler.Handler(request, cancellationToken);
                return CastResponse<TResponse>(response, requestType);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Request handler failed for '{requestType.FullName}'.", ex);
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

        private void AddRequestHandlerInternal(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            RequestRegistrationKey key = new RequestRegistrationKey(requestType, responseType, handler);
            if (requestHandlerIds.ContainsKey(key))
            {
                return;
            }

            long handlerId = nextRequestHandlerId++;
            RequestHandlerEntry entry = new RequestHandlerEntry(requestType, responseType, handlerId, handler);
            requestHandlers.Add(requestType, handlerId, entry);
            requestHandlerIds.Add(key, handlerId);
        }

        private void RemoveRequestHandlerInternal(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            RequestRegistrationKey key = new RequestRegistrationKey(requestType, responseType, handler);
            if (!requestHandlerIds.TryGetValue(key, out long handlerId))
            {
                return;
            }

            requestHandlers.Remove(requestType, handlerId);
            requestHandlerIds.Remove(key);
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
            EnsureNotNull(type, nameof(type));
            EnsureNotNull(evt, nameof(evt));

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

        private static Func<object, CancellationToken, Awaitable<object>> CreateGenericRequestAdapter<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler)
            where TRequest : ContextRequest<TResponse>
        {
            return async (request, cancellationToken) =>
            {
                TRequest typedRequest = (TRequest)request;
                TResponse response = await handler(typedRequest, cancellationToken);
                return response;
            };
        }

        private RequestHandlerEntry ResolveRequestHandler(Type requestType, Type responseType)
        {
            lock (sync)
            {
                IReadOnlyCollection<RequestHandlerEntry> candidates = GetRequestCandidates(requestType);
                RequestHandlerEntry? match = null;

                foreach (RequestHandlerEntry candidate in candidates)
                {
                    if (candidate.ResponseType != responseType)
                    {
                        continue;
                    }

                    if (match.HasValue)
                    {
                        throw new InvalidOperationException($"Multiple request handlers registered for '{requestType.FullName}'.");
                    }

                    match = candidate;
                }

                if (!match.HasValue)
                {
                    throw new InvalidOperationException($"No request handler registered for '{requestType.FullName}'.");
                }

                return match.Value;
            }
        }

        private IReadOnlyCollection<RequestHandlerEntry> GetRequestCandidates(Type requestType)
        {
            string indexerName = BuildIndexerName(RequestIndexerPrefix, requestType);
            if (!requestHandlers.TryGetIndexer(indexerName, out _))
            {
                requestHandlers.AddIndexer(indexerName, (declaredType, _) => declaredType == requestType);
            }

            return requestHandlers.GetIndexedValues(indexerName);
        }

        private static TResponse CastResponse<TResponse>(object response, Type requestType)
        {
            if (response is TResponse typed)
            {
                return typed;
            }

            if (response == null && default(TResponse) == null)
            {
                return default;
            }

            throw new InvalidCastException($"Request handler for '{requestType.FullName}' returned incompatible response type.");
        }

        private static void ValidateRequestHandlerArguments(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            EnsureNotNull(requestType, nameof(requestType));
            EnsureNotNull(responseType, nameof(responseType));
            EnsureNotNull(handler, nameof(handler));

            if (!TryResolveContextRequestResponseType(requestType, out Type declaredResponseType))
            {
                throw new ArgumentException($"Type '{requestType.FullName}' must inherit from {nameof(ContextRequest<object>)}.", nameof(requestType));
            }

            if (declaredResponseType != responseType)
            {
                throw new ArgumentException($"Request type '{requestType.FullName}' is bound to '{declaredResponseType.FullName}', not '{responseType.FullName}'.", nameof(responseType));
            }
        }

        private static bool TryResolveContextRequestResponseType(Type requestType, out Type responseType)
        {
            Type current = requestType;
            while (current != null)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ContextRequest<>))
                {
                    responseType = current.GetGenericArguments()[0];
                    return true;
                }

                current = current.BaseType;
            }

            responseType = null;
            return false;
        }

        private static void EnsureNotNull(object value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
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

        private readonly struct RequestHandlerEntry
        {
            public RequestHandlerEntry(Type requestType, Type responseType, long handlerId, Func<object, CancellationToken, Awaitable<object>> handler)
            {
                RequestType = requestType;
                ResponseType = responseType;
                HandlerId = handlerId;
                Handler = handler;
            }

            public Type RequestType { get; }
            public Type ResponseType { get; }
            public long HandlerId { get; }
            public Func<object, CancellationToken, Awaitable<object>> Handler { get; }
        }

        private readonly struct GenericRequestRegistration
        {
            public GenericRequestRegistration(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
            {
                RequestType = requestType;
                ResponseType = responseType;
                Handler = handler;
            }

            public Type RequestType { get; }
            public Type ResponseType { get; }
            public Func<object, CancellationToken, Awaitable<object>> Handler { get; }
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

        private readonly struct RequestRegistrationKey : IEquatable<RequestRegistrationKey>
        {
            public RequestRegistrationKey(Type requestType, Type responseType, Delegate handler)
            {
                RequestType = requestType;
                ResponseType = responseType;
                Handler = handler;
            }

            private Type RequestType { get; }
            private Type ResponseType { get; }
            private Delegate Handler { get; }

            public bool Equals(RequestRegistrationKey other)
            {
                return RequestType == other.RequestType && ResponseType == other.ResponseType && Equals(Handler, other.Handler);
            }

            public override bool Equals(object obj)
            {
                return obj is RequestRegistrationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(RequestType, ResponseType, Handler);
            }
        }
    }
}
