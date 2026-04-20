using System;

namespace Scaffold.CloudCode
{
    // sample: base providing IOptimisticCloudCodeHandler type identity for TRequest/TResponse.
    public abstract class OptimisticHandlerBase<TRequest, TResponse> : IOptimisticCloudCodeHandler, IRequestHandler<TRequest, TResponse> where TRequest : class
    {
        public Type RequestClrType => typeof(TRequest);

        public Type ResponseClrType => typeof(TResponse);

        public abstract bool TryMatch(string module, string endpoint, TRequest request);

        public abstract TResponse GetOptimisticResponse(TRequest request);

        public abstract void Validate(TResponse serverResponse, TResponse optimisticResponse);
    }
}
