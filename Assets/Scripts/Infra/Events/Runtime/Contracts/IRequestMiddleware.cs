using System;
using System.Threading;
using UnityEngine;

namespace Scaffold.Events
{
    public interface IRequestMiddleware
    {
        Awaitable<TResponse> Invoke<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken, Func<ContextRequest<TResponse>, CancellationToken, Awaitable<TResponse>> next);
    }
}
