using System;
using Scaffold.Maps;

namespace Scaffold.CloudCode
{
    internal sealed class CloudCodeOptimisticHandlerRegistry
    {
        private readonly Map<Type, Type, IRequestHandler> handlers = new Map<Type, Type, IRequestHandler>();

        internal void Register<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler) where TRequest : class
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type requestType = typeof(TRequest);
            Type responseType = typeof(TResponse);
            if (handlers.Contains(requestType, responseType))
            {
                throw new InvalidOperationException(
                    $"Duplicate optimistic handler for ({requestType.Name}, {responseType.Name}).");
            }

            handlers.Add(requestType, responseType, handler);
        }

        internal bool TryGetHandler(Type requestType, Type responseType, out IRequestHandler handler)
        {
            return handlers.TryGetValue(requestType, responseType, out handler);
        }
    }
}
