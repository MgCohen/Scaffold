using System;

namespace Scaffold.CloudCode
{
    // sample: discoverable optimistic handler; RequestClrType/ResponseClrType must match IRequestHandler<TRequest,TResponse>.
    public interface IOptimisticCloudCodeHandler : IRequestHandler
    {
        Type RequestClrType { get; }

        Type ResponseClrType { get; }
    }
}
