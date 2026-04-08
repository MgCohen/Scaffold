using System;

namespace Scaffold.CloudCode
{
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
