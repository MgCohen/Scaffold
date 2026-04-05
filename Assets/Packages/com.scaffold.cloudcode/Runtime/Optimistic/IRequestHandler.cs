using System;

namespace Scaffold.CloudCode
{
    /// <summary>
    /// Non-generic surface used for optimistic-handler lookup (registry stores <see cref="IRequestHandler"/>).
    /// </summary>
    public interface IRequestHandler
    {
        bool TryMatch(string module, string endpoint, object request);
    }

    /// <summary>
    /// Marker hierarchy level for response type; implement <see cref="IRequestHandler{TRequest, TResponse}"/> in application code.
    /// </summary>
    public interface IRequestHandler<TResponse> : IRequestHandler
    {
        TResponse GetOptimisticResponse(object request);

        /// <summary>sample: compare <paramref name="serverResponse"/> to <paramref name="optimisticResponse"/>; throw <see cref="OptimisticReconciliationException"/> (or another exception) on mismatch to route through <see cref="CloudCodeErrorHandler.Handle"/>.</summary>
        void Validate(TResponse serverResponse, TResponse optimisticResponse);
    }

    public interface IRequestHandler<TRequest, TResponse> : IRequestHandler<TResponse> where TRequest : class
    {
        bool TryMatch(string module, string endpoint, TRequest request);

        TResponse GetOptimisticResponse(TRequest request);

        bool IRequestHandler.TryMatch(string module, string endpoint, object request)
        {
            return request is TRequest typed && TryMatch(module, endpoint, typed);
        }

        TResponse IRequestHandler<TResponse>.GetOptimisticResponse(object request)
        {
            if (request is not TRequest typed)
            {
                throw new InvalidOperationException(
                    $"Request object must be {typeof(TRequest).Name}; got {request?.GetType().Name ?? "null"}.");
            }

            return GetOptimisticResponse(typed);
        }
    }
}
