using System;
using System.Collections.Generic;
using VContainer;

namespace Scaffold.CloudCode
{
    public sealed class CloudCodeOptimisticHandlerRegistry
    {
        private readonly IObjectResolver resolver;
        private readonly Dictionary<(Type Request, Type Response), IRequestHandler> handlers =
            new Dictionary<(Type Request, Type Response), IRequestHandler>();
        private readonly object gate = new object();

        public CloudCodeOptimisticHandlerRegistry(IObjectResolver resolver = null)
        {
            this.resolver = resolver;
        }

        public void Register<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler) where TRequest : class
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type requestType = typeof(TRequest);
            Type responseType = typeof(TResponse);
            (Type Request, Type Response) key = (requestType, responseType);
            lock (gate)
            {
                if (handlers.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        $"Duplicate optimistic handler for ({requestType.Name}, {responseType.Name}).");
                }

                handlers.Add(key, handler);
            }
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
                    return TryMatchAndGetOptimistic(module, endpoint, request, found, out handler, out optimisticResponse);
                }

                if (resolver == null)
                {
                    return false;
                }

                IEnumerable<IOptimisticCloudCodeHandler> discovered = resolver.Resolve<IEnumerable<IOptimisticCloudCodeHandler>>();
                if (discovered == null)
                {
                    return false;
                }

                foreach (IOptimisticCloudCodeHandler candidate in discovered)
                {
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate.RequestClrType != key.Request || candidate.ResponseClrType != key.Response)
                    {
                        continue;
                    }

                    if (!candidate.TryMatch(module, endpoint, request))
                    {
                        continue;
                    }

                    if (candidate is not IRequestHandler<TResponse> typedHandler)
                    {
                        continue;
                    }

                    if (!TryMatchAndGetOptimistic(module, endpoint, request, typedHandler, out handler, out optimisticResponse))
                    {
                        continue;
                    }

                    if (!handlers.ContainsKey(key))
                    {
                        handlers[key] = typedHandler;
                    }

                    return true;
                }

                return false;
            }
        }

        private static bool TryMatchAndGetOptimistic<TResponse>(
            string module,
            string endpoint,
            object request,
            IRequestHandler found,
            out IRequestHandler<TResponse> handler,
            out TResponse optimisticResponse)
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
    }
}
