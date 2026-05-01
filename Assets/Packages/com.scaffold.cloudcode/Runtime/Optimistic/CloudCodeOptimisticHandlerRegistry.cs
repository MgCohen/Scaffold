using System;
using System.Collections.Generic;
using VContainer;

namespace Scaffold.CloudCode
{
    public sealed class CloudCodeOptimisticHandlerRegistry
    {
        public CloudCodeOptimisticHandlerRegistry(IObjectResolver resolver = null)
        {
            this.resolver = resolver;
        }

        private readonly IObjectResolver resolver;
        private readonly Dictionary<(Type Request, Type Response), IRequestHandler> handlers = new Dictionary<(Type Request, Type Response), IRequestHandler>();
        private readonly object gate = new object();

        public void Register<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler) where TRequest : class
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            AddHandlerIfNew(handler);
        }

        public bool TryResolve<TResponse>(string module, string endpoint, object request, out IRequestHandler<TResponse> handler, out TResponse optimisticResponse)
        {
            handler = null;
            optimisticResponse = default;
            if (request == null)
            {
                return false;
            }

            (Type Request, Type Response) key = (request.GetType(), typeof(TResponse));

            lock (gate)
            {
                if (handlers.TryGetValue(key, out IRequestHandler found) && found != null)
                {
                    return TryParseOptimistic(module, endpoint, request, found, out handler, out optimisticResponse);
                }

                return TryDiscoverAndResolve(module, endpoint, request, key, out handler, out optimisticResponse);
            }
        }

        private void AddHandlerIfNew<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler) where TRequest : class
        {
            Type requestType = typeof(TRequest);
            Type responseType = typeof(TResponse);
            (Type Request, Type Response) key = (requestType, responseType);
            lock (gate)
            {
                if (handlers.ContainsKey(key))
                {
                    throw new InvalidOperationException($"Duplicate optimistic handler for ({requestType.Name}, {responseType.Name}).");
                }

                handlers.Add(key, handler);
            }
        }

        private bool TryDiscoverAndResolve<TResponse>(string module, string endpoint, object request, (Type Request, Type Response) key, out IRequestHandler<TResponse> handler, out TResponse optimisticResponse)
        {
            handler = null;
            optimisticResponse = default;
            if (resolver == null)
            {
                return false;
            }

            IEnumerable<IOptimisticCloudCodeHandler> discovered = resolver.Resolve<IEnumerable<IOptimisticCloudCodeHandler>>();
            if (discovered == null)
            {
                return false;
            }

            return TryResolveFromDiscoveredHandlers(module, endpoint, request, key, discovered, out handler, out optimisticResponse);
        }

        private bool TryResolveFromDiscoveredHandlers<TResponse>(string module, string endpoint, object request, (Type Request, Type Response) key, IEnumerable<IOptimisticCloudCodeHandler> discovered, out IRequestHandler<TResponse> handler, out TResponse optimisticResponse)
        {
            handler = null;
            optimisticResponse = default;
            foreach (IOptimisticCloudCodeHandler candidate in discovered)
            {
                if (TryConsumeDiscoveredCandidate(module, endpoint, request, key, candidate, out handler, out optimisticResponse))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryConsumeDiscoveredCandidate<TResponse>(string module, string endpoint, object request, (Type Request, Type Response) key, IOptimisticCloudCodeHandler candidate, out IRequestHandler<TResponse> handler, out TResponse optimisticResponse)
        {
            handler = null; optimisticResponse = default;
            if (!TryParseDiscoveredCandidate(candidate, key, out IRequestHandler<TResponse> typedHandler))
            {
                return false;
            }

            if (!typedHandler.TryMatch(module, endpoint, request))
            {
                return false;
            }

            if (!TryParseOptimistic(module, endpoint, request, typedHandler, out handler, out optimisticResponse))
            {
                return false;
            }

            RegisterHandlerIfAbsent(key, typedHandler);
            return true;
        }

        private bool TryParseOptimistic<TResponse>(string module, string endpoint, object request, IRequestHandler found, out IRequestHandler<TResponse> handler, out TResponse optimisticResponse)
        {
            handler = null;
            optimisticResponse = default;
            if (found is not IRequestHandler<TResponse> typedHandler)
            {
                return false;
            }

            if (!found.TryMatch(module, endpoint, request))
            {
                return false;
            }

            handler = typedHandler;
            optimisticResponse = typedHandler.GetOptimisticResponse(request);
            return true;
        }

        private void RegisterHandlerIfAbsent((Type Request, Type Response) key, IRequestHandler handler)
        {
            if (!handlers.ContainsKey(key))
            {
                handlers[key] = handler;
            }
        }

        private static bool TryParseDiscoveredCandidate<TResponse>(IOptimisticCloudCodeHandler candidate, (Type Request, Type Response) key, out IRequestHandler<TResponse> typedHandler)
        {
            typedHandler = null;
            if (candidate == null)
            {
                return false;
            }

            if (candidate.RequestClrType != key.Request || candidate.ResponseClrType != key.Response)
            {
                return false;
            }

            if (candidate is IRequestHandler<TResponse> typed)
            {
                typedHandler = typed;
                return true;
            }

            return false;
        }
    }
}
