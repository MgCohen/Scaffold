using System;
using System.Collections.Generic;
using System.Threading;
using Scaffold.Maps;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Scaffold.Events
{
    public class ScalableEventBus : IEventBus, IRequestBus
    {
        private const string exactIndexerPrefix = "exact:";
        private const string hierarchyIndexerPrefix = "hierarchy:";
        private const string requestIndexerPrefix = "request:";

        private readonly object sync = new object();
        private readonly Map<Type, long, EventSubscription> eventSubscriptions = new Map<Type, long, EventSubscription>();
        private readonly Map<Type, long, RequestSubscription> requestSubscriptions = new Map<Type, long, RequestSubscription>();
        private readonly Dictionary<EventSubscriptionKey, EventSubscription> eventRegistrationLookup = new Dictionary<EventSubscriptionKey, EventSubscription>();
        private readonly Dictionary<RequestSubscriptionKey, RequestSubscription> requestRegistrationLookup = new Dictionary<RequestSubscriptionKey, RequestSubscription>();
        private readonly List<IEventMiddleware> eventMiddlewares;
        private readonly List<IRequestMiddleware> requestMiddlewares;
        private readonly IEventDiagnosticsSink diagnosticsSink;

        private long nextEventSubscriptionId = 1;
        private long nextRequestSubscriptionId = 1;

        public ScalableEventBus(IEnumerable<IEventMiddleware> eventMiddlewares, IEnumerable<IRequestMiddleware> requestMiddlewares, IEventDiagnosticsSink diagnosticsSink)
        {
            this.eventMiddlewares = CopyMiddlewares(eventMiddlewares);
            this.requestMiddlewares = CopyMiddlewares(requestMiddlewares);
            this.diagnosticsSink = ResolveDiagnosticsSink(diagnosticsSink);
        }

        #region Events

        public void AddListener<T>(Action<T> evt) where T : ContextEvent
        {
            EnsureNotNull(evt, nameof(evt));
            Type eventType = typeof(T);
            Action<ContextEvent> dispatch = CreateListenerAdapter(evt);
            AddEventSubscription(eventType, evt, dispatch);
        }

        public void RemoveListener<T>(Action<T> evt) where T : ContextEvent
        {
            EnsureNotNull(evt, nameof(evt));
            RemoveEventSubscription(typeof(T), evt);
        }

        public void AddListener(Type type, Action<ContextEvent> evt)
        {
            ValidateOpenTypeArguments(type, evt);
            AddEventSubscription(type, evt, evt);
        }

        public void RemoveListener(Type type, Action<ContextEvent> evt)
        {
            ValidateOpenTypeArguments(type, evt);
            RemoveEventSubscription(type, evt);
        }

        public void Raise(ContextEvent evt)
        {
            EnsureNotNull(evt, nameof(evt));
            Type actualType = evt.GetType();
            List<EventSubscription> dispatch = CaptureEventDispatch(actualType);
            EventDispatchContext context = EventDispatchContext.ForEvent(actualType);
            diagnosticsSink.OnEventPublished(context, dispatch.Count);
            ExecuteEventPipeline(evt, dispatch, context, 0);
        }

        private void AddEventSubscription(Type eventType, Delegate originalCallback, Action<ContextEvent> dispatch)
        {
            lock (sync)
            {
                EventSubscriptionKey key = CreateEventSubscriptionKey(eventType, originalCallback);
                if (EventSubscriptionExists(key)) { return; }
                RegisterEventSubscription(eventType, key, originalCallback, dispatch);
            }
        }

        private void RemoveEventSubscription(Type eventType, Delegate originalCallback)
        {
            lock (sync)
            {
                EventSubscriptionKey key = CreateEventSubscriptionKey(eventType, originalCallback);
                bool found = TryGetEventSubscription(key, out EventSubscription subscription);
                if (!found) { return; }
                RemoveStoredEventSubscription(key, subscription);
            }
        }

        private List<EventSubscription> CaptureEventDispatch(Type actualType)
        {
            lock (sync)
            {
                IReadOnlyCollection<EventSubscription> exactListeners = GetExactListeners(actualType);
                IReadOnlyCollection<EventSubscription> hierarchyListeners = GetHierarchyListeners(actualType);
                return CreateDispatchList(exactListeners, hierarchyListeners);
            }
        }

        private IReadOnlyCollection<EventSubscription> GetExactListeners(Type actualType)
        {
            string indexerName = BuildIndexerName(exactIndexerPrefix, actualType);
            Func<Type, long, bool> predicate = (declaredType, _) => declaredType == actualType;
            return GetIndexedEventSubscriptions(indexerName, predicate);
        }

        private IReadOnlyCollection<EventSubscription> GetHierarchyListeners(Type actualType)
        {
            string indexerName = BuildIndexerName(hierarchyIndexerPrefix, actualType);
            Func<Type, long, bool> predicate = (declaredType, _) => declaredType != actualType && declaredType.IsAssignableFrom(actualType);
            return GetIndexedEventSubscriptions(indexerName, predicate);
        }

        private IReadOnlyCollection<EventSubscription> GetIndexedEventSubscriptions(string indexerName, Func<Type, long, bool> predicate)
        {
            EnsureEventIndexer(indexerName, predicate);
            return eventSubscriptions.GetIndexedValues(indexerName);
        }

        private void EnsureEventIndexer(string indexerName, Func<Type, long, bool> predicate)
        {
            bool exists = eventSubscriptions.TryGetIndexer(indexerName, out _);
            if (!exists)
            {
                eventSubscriptions.AddIndexer(indexerName, predicate);
            }
        }

        private void ExecuteEventPipeline(ContextEvent evt, List<EventSubscription> dispatch, EventDispatchContext context, int middlewareIndex)
        {
            bool atEnd = middlewareIndex >= eventMiddlewares.Count;
            if (atEnd) { InvokeEventDispatch(evt, dispatch, context); return; }
            InvokeEventMiddleware(evt, dispatch, context, middlewareIndex);
        }

        private void InvokeEventDispatch(ContextEvent evt, List<EventSubscription> dispatch, EventDispatchContext context)
        {
            foreach (EventSubscription subscription in dispatch)
            {
                InvokeEventSubscription(evt, subscription, context);
            }
        }

        private void InvokeEventSubscription(ContextEvent evt, EventSubscription subscription, EventDispatchContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            InvokeEventSubscriptionCore(evt, subscription, context);
            ReportEventInvocation(context, subscription.EventType, stopwatch);
        }

        private static Action<ContextEvent> CreateListenerAdapter<T>(Action<T> evt) where T : ContextEvent
        {
            return contextEvent => evt((T)contextEvent);
        }

        private static List<EventSubscription> CreateDispatchList(IReadOnlyCollection<EventSubscription> exactListeners, IReadOnlyCollection<EventSubscription> hierarchyListeners)
        {
            int capacity = exactListeners.Count + hierarchyListeners.Count;
            List<EventSubscription> dispatch = new List<EventSubscription>(capacity);
            AddDispatchEntries(dispatch, exactListeners);
            AddDispatchEntries(dispatch, hierarchyListeners);
            return dispatch;
        }

        private static void ValidateOpenTypeArguments(Type type, Action<ContextEvent> evt)
        {
            EnsureNotNull(type, nameof(type));
            EnsureNotNull(evt, nameof(evt));
            bool valid = typeof(ContextEvent).IsAssignableFrom(type);
            if (!valid)
            {
                throw new ArgumentException($"Type '{type.FullName}' must inherit from {nameof(ContextEvent)}.", nameof(type));
            }
        }

        #endregion

        #region Requests

        public void AddRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler)
            where TRequest : ContextRequest<TResponse>
        {
            EnsureNotNull(handler, nameof(handler));
            Type requestType = typeof(TRequest);
            Type responseType = typeof(TResponse);
            Func<object, CancellationToken, Awaitable<object>> dispatch = CreateRequestHandlerAdapter(handler);
            AddRequestSubscription(requestType, responseType, handler, dispatch);
        }

        public void RemoveRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler)
            where TRequest : ContextRequest<TResponse>
        {
            EnsureNotNull(handler, nameof(handler));
            RemoveRequestSubscription(typeof(TRequest), typeof(TResponse), handler);
        }

        public void AddRequestHandler(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            ValidateRequestHandlerArguments(requestType, responseType, handler);
            AddRequestSubscription(requestType, responseType, handler, handler);
        }

        public void RemoveRequestHandler(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            ValidateRequestHandlerArguments(requestType, responseType, handler);
            RemoveRequestSubscription(requestType, responseType, handler);
        }

        public async Awaitable<TResponse> RequestAsync<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            EnsureNotNull(request, nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            Type requestType = request.GetType();
            EventDispatchContext context = EventDispatchContext.ForRequest(requestType);
            return await ExecuteRequestWithDiagnostics(request, cancellationToken, requestType, context);
        }

        private void AddRequestSubscription(Type requestType, Type responseType, Delegate originalCallback, Func<object, CancellationToken, Awaitable<object>> dispatch)
        {
            lock (sync)
            {
                RequestSubscriptionKey key = CreateRequestSubscriptionKey(requestType, responseType, originalCallback);
                if (RequestSubscriptionExists(key)) { return; }
                RegisterRequestSubscription(requestType, responseType, key, originalCallback, dispatch);
            }
        }

        private void RemoveRequestSubscription(Type requestType, Type responseType, Delegate originalCallback)
        {
            lock (sync)
            {
                RequestSubscriptionKey key = CreateRequestSubscriptionKey(requestType, responseType, originalCallback);
                bool found = TryGetRequestSubscription(key, out RequestSubscription subscription);
                if (!found) { return; }
                RemoveStoredRequestSubscription(key, subscription);
            }
        }

        private static Func<object, CancellationToken, Awaitable<object>> CreateRequestHandlerAdapter<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler)
            where TRequest : ContextRequest<TResponse>
        {
            return async (request, cancellationToken) =>
            {
                TRequest typedRequest = (TRequest)request;
                TResponse response = await handler(typedRequest, cancellationToken);
                return response;
            };
        }

        private static TResponse CastResponse<TResponse>(object response, Type requestType)
        {
            bool matches = response is TResponse;
            if (matches) { return (TResponse)response; }
            return CastDefaultOrThrow<TResponse>(response, requestType);
        }

        private static void ValidateRequestHandlerArguments(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            EnsureNotNull(requestType, nameof(requestType));
            EnsureNotNull(responseType, nameof(responseType));
            EnsureNotNull(handler, nameof(handler));
            Type declaredResponseType = ResolveDeclaredResponseType(requestType);
            EnsureResponseTypeMatch(requestType, responseType, declaredResponseType);
        }

        private static Type ResolveDeclaredResponseType(Type requestType)
        {
            bool resolved = TryResolveContextRequestResponseType(requestType, out Type declaredResponseType);
            if (resolved)
            {
                return declaredResponseType;
            }

            string message = $"Type '{requestType.FullName}' must inherit from {nameof(ContextRequest<object>)}.";
            throw new ArgumentException(message, nameof(requestType));
        }

        private static void EnsureResponseTypeMatch(Type requestType, Type responseType, Type declaredResponseType)
        {
            bool matches = declaredResponseType == responseType;
            if (!matches)
            {
                string message = $"Request type '{requestType.FullName}' is bound to '{declaredResponseType.FullName}', not '{responseType.FullName}'.";
                throw new ArgumentException(message, nameof(responseType));
            }
        }

        private static bool TryResolveContextRequestResponseType(Type requestType, out Type responseType)
        {
            if (requestType == null) { responseType = null; return false; }
            bool resolved = TryResolveCurrentResponseType(requestType, out responseType);
            if (resolved) { return true; }
            return TryResolveContextRequestResponseType(requestType.BaseType, out responseType);
        }

        #endregion

        #region Middlewares

        public void Clear()
        {
            lock (sync)
            {
                ResetSubscriptions();
                ResetSubscriptionIds();
            }
        }

        private static List<TMiddleware> CopyMiddlewares<TMiddleware>(IEnumerable<TMiddleware> source)
        {
            List<TMiddleware> items = new List<TMiddleware>();
            if (source == null) { return items; }
            AddNonNullMiddlewares(source, items);
            return items;
        }

        #endregion

        #region Diagnostics

        private static IEventDiagnosticsSink ResolveDiagnosticsSink(IEventDiagnosticsSink diagnostics)
        {
            if (diagnostics != null)
            {
                return diagnostics;
            }

            return NoOpEventDiagnosticsSink.Instance;
        }

        #endregion

        private static string BuildIndexerName(string prefix, Type type)
        {
            string typeName = ResolveTypeName(type);
            return prefix + typeName;
        }

        private static void EnsureNotNull(object value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        private static EventSubscriptionKey CreateEventSubscriptionKey(Type eventType, Delegate originalCallback)
        {
            return new EventSubscriptionKey(eventType, originalCallback);
        }

        private bool EventSubscriptionExists(EventSubscriptionKey key)
        {
            return eventRegistrationLookup.ContainsKey(key);
        }

        private void RegisterEventSubscription(Type eventType, EventSubscriptionKey key, Delegate originalCallback, Action<ContextEvent> dispatch)
        {
            long subscriptionId = nextEventSubscriptionId++;
            EventSubscription subscription = new EventSubscription(eventType, subscriptionId, originalCallback, dispatch);
            eventSubscriptions.Add(eventType, subscriptionId, subscription);
            eventRegistrationLookup.Add(key, subscription);
        }

        private bool TryGetEventSubscription(EventSubscriptionKey key, out EventSubscription subscription)
        {
            return eventRegistrationLookup.TryGetValue(key, out subscription);
        }

        private void RemoveStoredEventSubscription(EventSubscriptionKey key, EventSubscription subscription)
        {
            eventSubscriptions.Remove(subscription.EventType, subscription.SubscriptionId);
            eventRegistrationLookup.Remove(key);
        }

        private void InvokeEventMiddleware(ContextEvent evt, List<EventSubscription> dispatch, EventDispatchContext context, int middlewareIndex)
        {
            IEventMiddleware middleware = eventMiddlewares[middlewareIndex];
            int nextIndex = middlewareIndex + 1;
            Action next = () => ExecuteEventPipeline(evt, dispatch, context, nextIndex);
            middleware.Invoke(evt, next);
        }

        private void InvokeEventSubscriptionCore(ContextEvent evt, EventSubscription subscription, EventDispatchContext context)
        {
            try { subscription.Dispatch(evt); }
            catch (Exception exception) { HandleEventSubscriptionFailure(context, exception); }
        }

        private void HandleEventSubscriptionFailure(EventDispatchContext context, Exception exception)
        {
            diagnosticsSink.OnListenerFailed(context, exception);
            Debug.LogException(exception);
        }

        private void ReportEventInvocation(EventDispatchContext context, Type eventType, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            double durationMs = stopwatch.Elapsed.TotalMilliseconds;
            diagnosticsSink.OnListenerInvoked(context, eventType, durationMs);
        }

        private static void AddDispatchEntries(List<EventSubscription> dispatch, IReadOnlyCollection<EventSubscription> subscriptions)
        {
            foreach (EventSubscription subscription in subscriptions)
            {
                dispatch.Add(subscription);
            }
        }

        private async Awaitable<TResponse> ExecuteRequestWithDiagnostics<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, Type requestType, EventDispatchContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try { return await ExecuteSuccessfulRequest(request, cancellationToken, requestType, context, stopwatch); }
            catch { ReportRequestCompletion(context, false, stopwatch); throw; }
        }

        private async Awaitable<TResponse> ExecuteSuccessfulRequest<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, Type requestType, EventDispatchContext context, Stopwatch stopwatch)
        {
            TResponse response = await ExecuteRequestCore(request, cancellationToken, requestType);
            ReportRequestCompletion(context, true, stopwatch);
            return response;
        }

        private async Awaitable<TResponse> ExecuteRequestCore<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, Type requestType)
        {
            return await ExecuteResolvedRequest(request, cancellationToken, requestType);
        }

        private async Awaitable<TResponse> ExecuteResolvedRequest<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, Type requestType)
        {
            RequestSubscription subscription = ResolveRequestSubscription(requestType, typeof(TResponse));
            return await ExecuteRequestPipeline(request, cancellationToken, subscription, requestType, 0);
        }

        private async Awaitable<TResponse> ExecuteRequestPipeline<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, RequestSubscription subscription, Type requestType, int middlewareIndex)
        {
            bool atEnd = middlewareIndex >= requestMiddlewares.Count;
            if (atEnd) { return await InvokeRequestSubscription(request, cancellationToken, subscription, requestType); }
            return await InvokeRequestMiddleware(request, cancellationToken, subscription, requestType, middlewareIndex);
        }

        private async Awaitable<TResponse> InvokeRequestSubscription<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, RequestSubscription subscription, Type requestType)
        {
            try { return await InvokeRequestSubscriptionCore(request, cancellationToken, subscription, requestType); }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { throw CreateHandlerFailureException(requestType, exception); }
        }

        private static InvalidOperationException CreateHandlerFailureException(Type requestType, Exception exception)
        {
            string message = $"Request handler failed for '{requestType.FullName}'.";
            return new InvalidOperationException(message, exception);
        }

        private RequestSubscription ResolveRequestSubscription(Type requestType, Type responseType)
        {
            lock (sync)
            {
                IReadOnlyCollection<RequestSubscription> candidates = GetRequestCandidates(requestType);
                return SelectSingleRequestHandler(candidates, requestType, responseType);
            }
        }

        private static RequestSubscription SelectSingleRequestHandler(IReadOnlyCollection<RequestSubscription> candidates, Type requestType, Type responseType)
        {
            RequestSubscription? match = FindMatchingRequestSubscription(candidates, requestType, responseType);
            EnsureRequestHandlerFound(match, requestType);
            return match.Value;
        }

        private IReadOnlyCollection<RequestSubscription> GetRequestCandidates(Type requestType)
        {
            string indexerName = BuildIndexerName(requestIndexerPrefix, requestType);
            EnsureRequestIndexer(indexerName, requestType);
            return requestSubscriptions.GetIndexedValues(indexerName);
        }

        private void EnsureRequestIndexer(string indexerName, Type requestType)
        {
            bool exists = requestSubscriptions.TryGetIndexer(indexerName, out _);
            if (!exists)
            {
                requestSubscriptions.AddIndexer(indexerName, (declaredType, _) => declaredType == requestType);
            }
        }

        private static RequestSubscriptionKey CreateRequestSubscriptionKey(Type requestType, Type responseType, Delegate originalCallback)
        {
            return new RequestSubscriptionKey(requestType, responseType, originalCallback);
        }

        private bool RequestSubscriptionExists(RequestSubscriptionKey key)
        {
            return requestRegistrationLookup.ContainsKey(key);
        }

        private void RegisterRequestSubscription(Type requestType, Type responseType, RequestSubscriptionKey key, Delegate originalCallback, Func<object, CancellationToken, Awaitable<object>> dispatch)
        {
            long subscriptionId = nextRequestSubscriptionId++;
            RequestSubscription subscription = new RequestSubscription(requestType, responseType, subscriptionId, originalCallback, dispatch);
            requestSubscriptions.Add(requestType, subscriptionId, subscription);
            requestRegistrationLookup.Add(key, subscription);
        }

        private bool TryGetRequestSubscription(RequestSubscriptionKey key, out RequestSubscription subscription)
        {
            return requestRegistrationLookup.TryGetValue(key, out subscription);
        }

        private void RemoveStoredRequestSubscription(RequestSubscriptionKey key, RequestSubscription subscription)
        {
            requestSubscriptions.Remove(subscription.RequestType, subscription.SubscriptionId);
            requestRegistrationLookup.Remove(key);
        }

        private async Awaitable<TResponse> InvokeRequestMiddleware<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, RequestSubscription subscription, Type requestType, int middlewareIndex)
        {
            IRequestMiddleware middleware = requestMiddlewares[middlewareIndex];
            int nextIndex = middlewareIndex + 1;
            Func<ContextRequest<TResponse>, CancellationToken, Awaitable<TResponse>> next = (nextRequest, nextCancellationToken) => ExecuteRequestPipeline(nextRequest, nextCancellationToken, subscription, requestType, nextIndex);
            return await middleware.Invoke(request, cancellationToken, next);
        }

        private static async Awaitable<TResponse> InvokeRequestSubscriptionCore<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, RequestSubscription subscription, Type requestType)
        {
            object response = await subscription.Dispatch(request, cancellationToken);
            TResponse typedResponse = CastResponse<TResponse>(response, requestType);
            return typedResponse;
        }

        private static RequestSubscription? FindMatchingRequestSubscription(IReadOnlyCollection<RequestSubscription> candidates, Type requestType, Type responseType)
        {
            RequestSubscription? match = null;
            foreach (RequestSubscription candidate in candidates) { match = TrySelectRequestCandidate(match, candidate, requestType, responseType); }
            return match;
        }

        private static RequestSubscription? TrySelectRequestCandidate(RequestSubscription? current, RequestSubscription candidate, Type requestType, Type responseType)
        {
            bool responseMatches = candidate.ResponseType == responseType;
            if (!responseMatches) { return current; }
            EnsureSingleRequestHandler(current, requestType);
            return candidate;
        }

        private static void EnsureSingleRequestHandler(RequestSubscription? current, Type requestType)
        {
            bool hasExisting = current.HasValue;
            if (hasExisting) { throw new InvalidOperationException($"Multiple request handlers registered for '{requestType.FullName}'."); }
        }

        private static void EnsureRequestHandlerFound(RequestSubscription? match, Type requestType)
        {
            bool found = match.HasValue;
            if (!found) { throw new InvalidOperationException($"No request handler registered for '{requestType.FullName}'."); }
        }

        private static TResponse CastDefaultOrThrow<TResponse>(object response, Type requestType)
        {
            bool allowsNull = response == null && default(TResponse) == null;
            if (allowsNull) { return default; }
            string message = $"Request handler for '{requestType.FullName}' returned incompatible response type.";
            throw new InvalidCastException(message);
        }

        private static bool TryResolveCurrentResponseType(Type requestType, out Type responseType)
        {
            bool isContextRequest = requestType.IsGenericType && requestType.GetGenericTypeDefinition() == typeof(ContextRequest<>);
            if (!isContextRequest) { responseType = null; return false; }
            Type[] arguments = requestType.GetGenericArguments();
            responseType = arguments[0];
            return true;
        }

        private void ResetSubscriptions()
        {
            eventSubscriptions.Clear();
            requestSubscriptions.Clear();
            eventRegistrationLookup.Clear();
            requestRegistrationLookup.Clear();
        }

        private void ResetSubscriptionIds()
        {
            nextEventSubscriptionId = 1;
            nextRequestSubscriptionId = 1;
        }

        private static void AddNonNullMiddlewares<TMiddleware>(IEnumerable<TMiddleware> source, List<TMiddleware> items)
        {
            foreach (TMiddleware middleware in source)
            {
                bool exists = middleware != null;
                if (exists) { items.Add(middleware); }
            }
        }

        private static string ResolveTypeName(Type type)
        {
            string assemblyQualifiedName = type.AssemblyQualifiedName;
            if (assemblyQualifiedName != null) { return assemblyQualifiedName; }
            return ResolveFallbackTypeName(type);
        }

        private static string ResolveFallbackTypeName(Type type)
        {
            string fullName = type.FullName;
            if (fullName != null) { return fullName; }
            return type.Name;
        }

        private void ReportRequestCompletion(EventDispatchContext context, bool success, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            double durationMs = stopwatch.Elapsed.TotalMilliseconds;
            diagnosticsSink.OnRequestCompleted(context, success, durationMs);
        }

        private readonly struct EventSubscription
        {
            public EventSubscription(Type eventType, long subscriptionId, Delegate originalCallback, Action<ContextEvent> dispatch)
            {
                EventType = eventType;
                SubscriptionId = subscriptionId;
                OriginalCallback = originalCallback;
                Dispatch = dispatch;
            }

            public Type EventType { get; }
            public long SubscriptionId { get; }
            public Delegate OriginalCallback { get; }
            public Action<ContextEvent> Dispatch { get; }
        }

        private readonly struct RequestSubscription
        {
            public RequestSubscription(Type requestType, Type responseType, long subscriptionId, Delegate originalCallback, Func<object, CancellationToken, Awaitable<object>> dispatch)
            {
                RequestType = requestType;
                ResponseType = responseType;
                SubscriptionId = subscriptionId;
                OriginalCallback = originalCallback;
                Dispatch = dispatch;
            }

            public Type RequestType { get; }
            public Type ResponseType { get; }
            public long SubscriptionId { get; }
            public Delegate OriginalCallback { get; }
            public Func<object, CancellationToken, Awaitable<object>> Dispatch { get; }
        }

        private readonly struct EventSubscriptionKey : IEquatable<EventSubscriptionKey>
        {
            public EventSubscriptionKey(Type eventType, Delegate callback)
            {
                EventType = eventType;
                Callback = callback;
            }

            private Type EventType { get; }
            private Delegate Callback { get; }

            public bool Equals(EventSubscriptionKey other)
            {
                return EventType == other.EventType && Equals(Callback, other.Callback);
            }

            public override bool Equals(object obj)
            {
                return obj is EventSubscriptionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(EventType, Callback);
            }
        }

        private readonly struct RequestSubscriptionKey : IEquatable<RequestSubscriptionKey>
        {
            public RequestSubscriptionKey(Type requestType, Type responseType, Delegate callback)
            {
                RequestType = requestType;
                ResponseType = responseType;
                Callback = callback;
            }

            private Type RequestType { get; }
            private Type ResponseType { get; }
            private Delegate Callback { get; }

            public bool Equals(RequestSubscriptionKey other)
            {
                return RequestType == other.RequestType && ResponseType == other.ResponseType && Equals(Callback, other.Callback);
            }

            public override bool Equals(object obj)
            {
                return obj is RequestSubscriptionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(RequestType, ResponseType, Callback);
            }
        }
    }
}
