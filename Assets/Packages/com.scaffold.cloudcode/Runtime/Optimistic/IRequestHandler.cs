using System;

namespace Scaffold.CloudCode
{
    public interface IRequestHandler
    {
        bool TryMatch(string module, string endpoint, object request);
    }

    public interface IRequestHandler<TResponse> : IRequestHandler
    {
        TResponse GetOptimisticResponse(object request);

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
