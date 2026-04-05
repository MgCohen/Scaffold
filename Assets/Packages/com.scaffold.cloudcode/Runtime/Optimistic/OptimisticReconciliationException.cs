using System;

namespace Scaffold.CloudCode
{
    /// <summary>
    /// Thrown from <see cref="IRequestHandler{TResponse}.Validate"/> when the server response does not reconcile with the optimistic value.
    /// <see cref="CloudCodeErrorHandler.Handle"/> receives this (or any exception) for trailing-call failures.
    /// </summary>
    public sealed class OptimisticReconciliationException : Exception
    {
        public OptimisticReconciliationException(string message, object serverResponse, object optimisticResponse) : base(message)
        {
            ServerResponse = serverResponse;
            OptimisticResponse = optimisticResponse;
        }

        public OptimisticReconciliationException(string message, object serverResponse, object optimisticResponse, Exception innerException) : base(message, innerException)
        {
            ServerResponse = serverResponse;
            OptimisticResponse = optimisticResponse;
        }

        public object ServerResponse { get; }

        public object OptimisticResponse { get; }
    }
}
