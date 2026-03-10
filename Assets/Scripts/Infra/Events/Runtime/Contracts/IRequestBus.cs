using System;
using System.Threading;
using UnityEngine;

namespace Scaffold.Events
{
    public interface IRequestBus
    {
        void AddRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler)
            where TRequest : ContextRequest<TResponse>;

        void RemoveRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, Awaitable<TResponse>> handler)
            where TRequest : ContextRequest<TResponse>;

        void AddRequestHandler(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler);
        void RemoveRequestHandler(Type requestType, Type responseType, Func<object, CancellationToken, Awaitable<object>> handler);
        Awaitable<TResponse> RequestAsync<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken = default);
    }
}
