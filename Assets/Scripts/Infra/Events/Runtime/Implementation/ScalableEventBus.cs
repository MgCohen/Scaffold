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
        private readonly Map<Type, long, ListenerEntry> listeners = new Map<Type, long, ListenerEntry>();
        private readonly Map<Type, long, RequestHandlerEntry> requestHandlers = new Map<Type, long, RequestHandlerEntry>();
        private readonly Dictionary<Delegate, GenericRegistration> genericRegistrations = new Dictionary<Delegate, GenericRegistration>();
        private readonly Dictionary<ListenerRegistrationKey, long> listenerIds = new Dictionary<ListenerRegistrationKey, long>();
        private readonly Dictionary<Delegate, GenericRequestRegistration> genericRequestRegistrations = new Dictionary<Delegate, GenericRequestRegistration>();
        private readonly Dictionary<RequestRegistrationKey, long> requestHandlerIds = new Dictionary<RequestRegistrationKey, long>();
        private readonly List<IEventMiddleware> eventMiddlewares;
        private readonly List<IRequestMiddleware> requestMiddlewares;
        private readonly IEventDiagnosticsSink diagnosticsSink;

        private long nextListenerId = 1;
        private long nextRequestHandlerId = 1;

        public ScalableEventBus()
            : this(null, null, null)
        {
        }

        public ScalableEventBus(IEnumerable<IEventMiddleware> eventMiddlewares, IEnumerable<IRequestMiddleware> requestMiddlewares, IEventDiagnosticsSink diagnosticsSink)
        {
            this.eventMiddlewares = CopyMiddlewares(eventMiddlewares);
            this.requestMiddlewares = CopyMiddlewares(requestMiddlewares);
            this.diagnosticsSink = ResolveDiagnosticsSink(diagnosticsSink);
        }

        public void AddListener<T>(Action<T> evt) where T : ContextEvent
        {
            EnsureNotNull(evt, nameof(evt));
            Type eventType = typeof(T);

            lock (sync)
            {
                AddGenericListenerIfMissing(eventType, evt);
            }
        }

        public void RemoveListener<T>(Action<T> evt) where T : ContextEvent
        {
            EnsureNotNull(evt, nameof(evt));

            lock (sync)
            {
                RemoveGenericListenerIfRegistered(evt);
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
            List<ListenerEntry> dispatch = CaptureDispatch(actualType);
            EventDispatchContext context = EventDispatchContext.ForEvent(actualType);
            diagnosticsSink.OnEventPublished(context, dispatch.Count);
            ExecuteEventPipeline(evt, dispatch, context, 0);
        }

        public void Clear()
        {
            lock (sync)
            {
                ResetState();
            }
        }

        public void AddRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler)
            where TRequest : ContextRequest<TResponse>
        {
            EnsureNotNull(handler, nameof(handler));
            Type requestType = typeof(TRequest);
            Type responseType = typeof(TResponse);

            lock (sync)
            {
                AddGenericRequestHandlerIfMissing(handler, requestType, responseType);
            }
        }

        public void RemoveRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler)
            where TRequest : ContextRequest<TResponse>
        {
            EnsureNotNull(handler, nameof(handler));

            lock (sync)
            {
                RemoveGenericRequestHandlerIfRegistered(handler);
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
            EventDispatchContext context = EventDispatchContext.ForRequest(requestType);
            TResponse response = await ExecuteRequestWithDiagnostics(request, cancellationToken, requestType, context);
            return response;
        }

        private void AddGenericListenerIfMissing<T>(Type eventType, Action<T> evt) where T : ContextEvent
        {
            bool exists = genericRegistrations.ContainsKey(evt);
            if (exists)
            {
                return;
            }

            Action<ContextEvent> adapter = CreateListenerAdapter(evt);
            RegisterGenericListener(evt, eventType, adapter);
        }

        private static Action<ContextEvent> CreateListenerAdapter<T>(Action<T> evt) where T : ContextEvent
        {
            Action<ContextEvent> adapter = e => evt((T)e);
            return adapter;
        }

        private void RegisterGenericListener<T>(Action<T> original, Type eventType, Action<ContextEvent> adapter) where T : ContextEvent
        {
            AddListenerInternal(eventType, adapter);
            GenericRegistration registration = new GenericRegistration(eventType, adapter);
            genericRegistrations.Add(original, registration);
        }

        private void RemoveGenericListenerIfRegistered(Delegate evt)
        {
            bool found = genericRegistrations.TryGetValue(evt, out GenericRegistration registration);
            if (!found)
            {
                return;
            }

            RemoveListenerInternal(registration.EventType, registration.Listener);
            genericRegistrations.Remove(evt);
        }

        private void AddGenericRequestHandlerIfMissing<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler, Type requestType, Type responseType)
            where TRequest : ContextRequest<TResponse>
        {
            bool exists = genericRequestRegistrations.ContainsKey(handler);
            if (exists)
            {
                return;
            }

            Func<object, CancellationToken, Awaitable<object>> adapter = CreateGenericRequestAdapter(handler);
            RegisterGenericRequestHandler(handler, requestType, responseType, adapter);
        }

        private void RegisterGenericRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> original, Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> adapter)
            where TRequest : ContextRequest<TResponse>
        {
            AddRequestHandlerInternal(requestType, responseType, adapter);
            GenericRequestRegistration registration = new GenericRequestRegistration(requestType, responseType, adapter);
            genericRequestRegistrations.Add(original, registration);
        }

        private void RemoveGenericRequestHandlerIfRegistered(Delegate handler)
        {
            bool found = genericRequestRegistrations.TryGetValue(handler, out GenericRequestRegistration registration);
            if (!found)
            {
                return;
            }

            RemoveRequestHandlerInternal(registration.RequestType, registration.ResponseType, registration.Handler);
            genericRequestRegistrations.Remove(handler);
        }

        private void ResetState()
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

        private async Awaitable<TResponse> ExecuteRequestWithDiagnostics<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, Type requestType, EventDispatchContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            TResponse response = await ExecuteRequestAndReport(request, cancellationToken, requestType, context, stopwatch);
            return response;
        }

        private async Awaitable<TResponse> ExecuteRequestAndReport<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, Type requestType, EventDispatchContext context, Stopwatch stopwatch)
        {
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
            RequestHandlerEntry handler = ResolveRequestHandler(requestType, typeof(TResponse));
            TResponse response = await ExecuteRequestPipeline(request, cancellationToken, handler, requestType, 0);
            return response;
        }

        private void ReportRequestCompletion(EventDispatchContext context, bool success, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            double durationMs = ToMilliseconds(stopwatch);
            diagnosticsSink.OnRequestCompleted(context, success, durationMs);
        }

        private void AddListenerInternal(Type type, Action<ContextEvent> evt)
        {
            ListenerRegistrationKey key = new ListenerRegistrationKey(type, evt);
            bool exists = listenerIds.ContainsKey(key);
            if (exists)
            {
                return;
            }

            RegisterListener(type, evt, key);
        }

        private void RegisterListener(Type type, Action<ContextEvent> evt, ListenerRegistrationKey key)
        {
            long listenerId = nextListenerId++;
            ListenerEntry entry = new ListenerEntry(type, listenerId, evt);
            listeners.Add(type, listenerId, entry);
            listenerIds.Add(key, listenerId);
        }

        private void RemoveListenerInternal(Type type, Action<ContextEvent> evt)
        {
            ListenerRegistrationKey key = new ListenerRegistrationKey(type, evt);
            bool found = listenerIds.TryGetValue(key, out long listenerId);
            if (!found)
            {
                return;
            }

            RemoveRegisteredListener(type, key, listenerId);
        }

        private void RemoveRegisteredListener(Type type, ListenerRegistrationKey key, long listenerId)
        {
            listeners.Remove(type, listenerId);
            listenerIds.Remove(key);
        }

        private void AddRequestHandlerInternal(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            RequestRegistrationKey key = new RequestRegistrationKey(requestType, responseType, handler);
            bool exists = requestHandlerIds.ContainsKey(key);
            if (exists)
            {
                return;
            }

            RegisterRequestHandler(requestType, responseType, handler, key);
        }

        private void RegisterRequestHandler(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler, RequestRegistrationKey key)
        {
            long handlerId = nextRequestHandlerId++;
            RequestHandlerEntry entry = new RequestHandlerEntry(requestType, responseType, handlerId, handler);
            requestHandlers.Add(requestType, handlerId, entry);
            requestHandlerIds.Add(key, handlerId);
        }

        private void RemoveRequestHandlerInternal(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler)
        {
            RequestRegistrationKey key = new RequestRegistrationKey(requestType, responseType, handler);
            bool found = requestHandlerIds.TryGetValue(key, out long handlerId);
            if (!found)
            {
                return;
            }

            RemoveRegisteredRequestHandler(requestType, key, handlerId);
        }

        private void RemoveRegisteredRequestHandler(Type requestType, RequestRegistrationKey key, long handlerId)
        {
            requestHandlers.Remove(requestType, handlerId);
            requestHandlerIds.Remove(key);
        }
        private List<ListenerEntry> CaptureDispatch(Type actualType)
        {
            lock (sync)
            {
                IReadOnlyCollection<ListenerEntry> exactListeners = GetExactListeners(actualType);
                IReadOnlyCollection<ListenerEntry> hierarchyListeners = GetHierarchyListeners(actualType);
                List<ListenerEntry> dispatch = CreateDispatchList(exactListeners, hierarchyListeners);
                return dispatch;
            }
        }

        private static List<ListenerEntry> CreateDispatchList(IReadOnlyCollection<ListenerEntry> exactListeners, IReadOnlyCollection<ListenerEntry> hierarchyListeners)
        {
            int capacity = exactListeners.Count + hierarchyListeners.Count;
            List<ListenerEntry> dispatch = new List<ListenerEntry>(capacity);
            AddEntries(dispatch, exactListeners);
            AddEntries(dispatch, hierarchyListeners);
            return dispatch;
        }

        private IReadOnlyCollection<ListenerEntry> GetExactListeners(Type actualType)
        {
            string indexerName = BuildIndexerName(exactIndexerPrefix, actualType);
            Func<Type, long, bool> predicate = (declaredType, _) => declaredType == actualType;
            IReadOnlyCollection<ListenerEntry> entries = GetIndexedListeners(indexerName, predicate);
            return entries;
        }

        private IReadOnlyCollection<ListenerEntry> GetHierarchyListeners(Type actualType)
        {
            string indexerName = BuildIndexerName(hierarchyIndexerPrefix, actualType);
            Func<Type, long, bool> predicate = (declaredType, _) => declaredType != actualType && declaredType.IsAssignableFrom(actualType);
            IReadOnlyCollection<ListenerEntry> entries = GetIndexedListeners(indexerName, predicate);
            return entries;
        }

        private IReadOnlyCollection<ListenerEntry> GetIndexedListeners(string indexerName, Func<Type, long, bool> predicate)
        {
            EnsureListenerIndexer(indexerName, predicate);
            IReadOnlyCollection<ListenerEntry> entries = listeners.GetIndexedValues(indexerName);
            return entries;
        }

        private void EnsureListenerIndexer(string indexerName, Func<Type, long, bool> predicate)
        {
            bool exists = listeners.TryGetIndexer(indexerName, out _);
            if (exists)
            {
                return;
            }

            listeners.AddIndexer(indexerName, predicate);
        }

        private static void AddEntries(List<ListenerEntry> dispatch, IReadOnlyCollection<ListenerEntry> entries)
        {
            foreach (ListenerEntry entry in entries)
            {
                dispatch.Add(entry);
            }
        }

        private void ExecuteEventPipeline(ContextEvent evt, List<ListenerEntry> dispatch, EventDispatchContext context, int middlewareIndex)
        {
            bool atEnd = middlewareIndex >= eventMiddlewares.Count;
            if (atEnd)
            {
                InvokeDispatch(dispatch, evt, context);
                return;
            }

            InvokeEventMiddleware(evt, dispatch, context, middlewareIndex);
        }

        private void InvokeEventMiddleware(ContextEvent evt, List<ListenerEntry> dispatch, EventDispatchContext context, int middlewareIndex)
        {
            IEventMiddleware middleware = eventMiddlewares[middlewareIndex];
            int nextIndex = middlewareIndex + 1;
            Action next = () => ExecuteEventPipeline(evt, dispatch, context, nextIndex);
            middleware.Invoke(evt, next);
        }

        private void InvokeDispatch(List<ListenerEntry> dispatch, ContextEvent evt, EventDispatchContext context)
        {
            foreach (ListenerEntry entry in dispatch)
            {
                TryInvokeListener(entry, evt, context);
            }
        }

        private void TryInvokeListener(ListenerEntry entry, ContextEvent evt, EventDispatchContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            InvokeListenerSafely(entry, evt, context);
            ReportListenerInvoked(context, entry.EventType, stopwatch);
        }

        private void InvokeListenerSafely(ListenerEntry entry, ContextEvent evt, EventDispatchContext context)
        {
            try { entry.Listener.Invoke(evt); }
            catch (Exception ex) { HandleListenerFailure(context, ex); }
        }

        private void HandleListenerFailure(EventDispatchContext context, Exception exception)
        {
            diagnosticsSink.OnListenerFailed(context, exception);
            UnityEngine.Debug.LogException(exception);
        }

        private void ReportListenerInvoked(EventDispatchContext context, Type declaredType, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            double durationMs = ToMilliseconds(stopwatch);
            diagnosticsSink.OnListenerInvoked(context, declaredType, durationMs);
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

        private static string BuildIndexerName(string prefix, Type type)
        {
            string name = type.AssemblyQualifiedName;
            if (name == null)
            {
                name = ResolveFallbackTypeName(type);
            }

            return prefix + name;
        }

        private static string ResolveFallbackTypeName(Type type)
        {
            string name = type.FullName;
            if (name != null)
            {
                return name;
            }

            return type.Name;
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

        private async Awaitable<TResponse> ExecuteRequestPipeline<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, RequestHandlerEntry handler, Type requestType, int middlewareIndex)
        {
            if (middlewareIndex >= requestMiddlewares.Count) { return await InvokeRequestHandler(request, cancellationToken, handler, requestType); }
            return await InvokeRequestMiddleware(request, cancellationToken, handler, requestType, middlewareIndex);
        }

        private async Awaitable<TResponse> InvokeRequestMiddleware<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, RequestHandlerEntry handler, Type requestType, int middlewareIndex)
        {
            IRequestMiddleware middleware = requestMiddlewares[middlewareIndex];
            int nextIndex = middlewareIndex + 1;
            Func<ContextRequest<TResponse>, CancellationToken, Awaitable<TResponse>> next = (nextRequest, nextCancellationToken) => ExecuteRequestPipeline(nextRequest, nextCancellationToken, handler, requestType, nextIndex);
            TResponse response = await middleware.Invoke(request, cancellationToken, next);
            return response;
        }

        private async Awaitable<TResponse> InvokeRequestHandler<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, RequestHandlerEntry handler, Type requestType)
        {
            try { return await InvokeRequestHandlerCore<TResponse>(request, cancellationToken, handler, requestType); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw CreateHandlerFailureException(requestType, ex); }
        }

        private static async Awaitable<TResponse> InvokeRequestHandlerCore<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, RequestHandlerEntry handler, Type requestType)
        {
            object response = await handler.Handler(request, cancellationToken);
            TResponse typedResponse = CastResponse<TResponse>(response, requestType);
            return typedResponse;
        }

        private static InvalidOperationException CreateHandlerFailureException(Type requestType, Exception exception)
        {
            string message = $"Request handler failed for '{requestType.FullName}'.";
            InvalidOperationException wrapped = new InvalidOperationException(message, exception);
            return wrapped;
        }

        private RequestHandlerEntry ResolveRequestHandler(Type requestType, Type responseType)
        {
            lock (sync)
            {
                IReadOnlyCollection<RequestHandlerEntry> candidates = GetRequestCandidates(requestType);
                RequestHandlerEntry handler = SelectSingleRequestHandler(candidates, requestType, responseType);
                return handler;
            }
        }
        private static RequestHandlerEntry SelectSingleRequestHandler(IReadOnlyCollection<RequestHandlerEntry> candidates, Type requestType, Type responseType)
        {
            RequestHandlerEntry? match = FindMatchingHandler(candidates, requestType, responseType);
            EnsureRequestHandlerFound(match, requestType);
            return match.Value;
        }

        private static RequestHandlerEntry? FindMatchingHandler(IReadOnlyCollection<RequestHandlerEntry> candidates, Type requestType, Type responseType)
        {
            RequestHandlerEntry? match = null;
            foreach (RequestHandlerEntry candidate in candidates) { match = TrySelectCandidate(match, candidate, requestType, responseType); }
            return match;
        }

        private static RequestHandlerEntry? TrySelectCandidate(RequestHandlerEntry? current, RequestHandlerEntry candidate, Type requestType, Type responseType)
        {
            bool responseMatches = candidate.ResponseType == responseType;
            if (!responseMatches)
            {
                return current;
            }

            EnsureSingleRequestHandler(current, requestType);
            return candidate;
        }

        private static void EnsureSingleRequestHandler(RequestHandlerEntry? current, Type requestType)
        {
            bool hasExisting = current.HasValue;
            if (hasExisting)
            {
                throw new InvalidOperationException($"Multiple request handlers registered for '{requestType.FullName}'.");
            }
        }

        private static void EnsureRequestHandlerFound(RequestHandlerEntry? match, Type requestType)
        {
            bool found = match.HasValue;
            if (!found)
            {
                throw new InvalidOperationException($"No request handler registered for '{requestType.FullName}'.");
            }
        }

        private IReadOnlyCollection<RequestHandlerEntry> GetRequestCandidates(Type requestType)
        {
            string indexerName = BuildIndexerName(requestIndexerPrefix, requestType);
            EnsureRequestIndexer(indexerName, requestType);
            IReadOnlyCollection<RequestHandlerEntry> entries = requestHandlers.GetIndexedValues(indexerName);
            return entries;
        }

        private void EnsureRequestIndexer(string indexerName, Type requestType)
        {
            bool exists = requestHandlers.TryGetIndexer(indexerName, out _);
            if (exists)
            {
                return;
            }

            requestHandlers.AddIndexer(indexerName, (declaredType, _) => declaredType == requestType);
        }

        private static TResponse CastResponse<TResponse>(object response, Type requestType)
        {
            bool matches = response is TResponse;
            if (matches)
            {
                TResponse typed = (TResponse)response;
                return typed;
            }

            return CastOrDefaultResponse<TResponse>(response, requestType);
        }

        private static TResponse CastOrDefaultResponse<TResponse>(object response, Type requestType)
        {
            bool allowsNull = response == null && default(TResponse) == null;
            if (allowsNull)
            {
                return default;
            }

            string message = $"Request handler for '{requestType.FullName}' returned incompatible response type.";
            throw new InvalidCastException(message);
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
            if (resolved) { return declaredResponseType; }
            throw CreateRequestTypeArgumentException(requestType);
        }

        private static ArgumentException CreateRequestTypeArgumentException(Type requestType)
        {
            string message = $"Type '{requestType.FullName}' must inherit from {nameof(ContextRequest<object>)}.";
            ArgumentException exception = new ArgumentException(message, nameof(requestType));
            return exception;
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
            return TryResolveFromTypeHierarchy(requestType, out responseType);
        }

        private static bool TryResolveFromTypeHierarchy(Type current, out Type responseType)
        {
            if (current == null) { responseType = null; return false; }
            bool found = TryResolveCurrentResponseType(current, out responseType);
            if (found) { return true; }
            Type baseType = current.BaseType;
            return TryResolveFromTypeHierarchy(baseType, out responseType);
        }

        private static bool TryResolveCurrentResponseType(Type current, out Type responseType)
        {
            bool isContextRequest = current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ContextRequest<>);
            if (!isContextRequest) { responseType = null; return false; }
            Type[] arguments = current.GetGenericArguments();
            responseType = arguments[0];
            return true;
        }

        private static void EnsureNotNull(object value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        private static List<TMiddleware> CopyMiddlewares<TMiddleware>(IEnumerable<TMiddleware> source)
        {
            List<TMiddleware> items = new List<TMiddleware>();
            if (source == null)
            {
                return items;
            }

            AddNonNullMiddlewares(source, items);
            return items;
        }

        private static void AddNonNullMiddlewares<TMiddleware>(IEnumerable<TMiddleware> source, List<TMiddleware> items)
        {
            foreach (TMiddleware middleware in source)
            {
                bool exists = middleware != null;
                if (exists)
                {
                    items.Add(middleware);
                }
            }
        }

        private static IEventDiagnosticsSink ResolveDiagnosticsSink(IEventDiagnosticsSink diagnostics)
        {
            if (diagnostics != null)
            {
                return diagnostics;
            }

            return NoOpEventDiagnosticsSink.Instance;
        }

        private static double ToMilliseconds(Stopwatch stopwatch)
        {
            return stopwatch.Elapsed.TotalMilliseconds;
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
