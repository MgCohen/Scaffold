using System;
using System.Collections.Generic;

namespace Scaffold.CloudCode
{
    /// <summary>
    /// Non-generic surface used for optimistic-handler lookup and dispatch (registry stores <see cref="IRequestHandler"/>).
    /// </summary>
    public interface IRequestHandler
    {
        bool TryMatch(string module, string endpoint, object request);

        object GetOptimisticResponse(object request);

        void OnDeferredServerResponse(object request, object optimisticResponse, object serverResponse, IReadOnlyDictionary<string, object> wirePayload);
    }

    /// <summary>
    /// Marker hierarchy level for response type; implement <see cref="IRequestHandler{TRequest, TResponse}"/> in application code.
    /// </summary>
    public interface IRequestHandler<TResponse> : IRequestHandler
    {
    }

    public interface IRequestHandler<TRequest, TResponse> : IRequestHandler<TResponse> where TRequest : class
    {
        bool TryMatch(string module, string endpoint, TRequest request);

        TResponse GetOptimisticResponse(TRequest request);

        void OnDeferredServerResponse(TRequest request, TResponse optimisticResponse, TResponse serverResponse, IReadOnlyDictionary<string, object> wirePayload);

        bool IRequestHandler.TryMatch(string module, string endpoint, object request)
        {
            return request is TRequest typed && TryMatch(module, endpoint, typed);
        }

        object IRequestHandler.GetOptimisticResponse(object request)
        {
            if (request is not TRequest typed)
            {
                throw new InvalidOperationException(
                    $"Request object must be {typeof(TRequest).Name}; got {request?.GetType().Name ?? "null"}.");
            }

            return GetOptimisticResponse(typed);
        }

        void IRequestHandler.OnDeferredServerResponse(object request, object optimisticResponse, object serverResponse, IReadOnlyDictionary<string, object> wirePayload)
        {
            OnDeferredServerResponse((TRequest)request, (TResponse)optimisticResponse, (TResponse)serverResponse, wirePayload);
        }
    }
}
