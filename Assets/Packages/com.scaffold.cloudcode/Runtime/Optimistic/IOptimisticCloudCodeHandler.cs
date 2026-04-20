using System;

namespace Scaffold.CloudCode
{
    /// <summary>
    /// Marks a handler discoverable by <see cref="CloudCodeOptimisticHandlerRegistry"/> via the DI container.
    /// Pair <see cref="RequestClrType"/> / <see cref="ResponseClrType"/> must match the closed <see cref="IRequestHandler{TRequest,TResponse}"/> implementation.
    /// </summary>
    public interface IOptimisticCloudCodeHandler : IRequestHandler
    {
        Type RequestClrType { get; }

        Type ResponseClrType { get; }
    }
}
