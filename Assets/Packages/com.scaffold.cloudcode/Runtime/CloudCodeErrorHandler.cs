using System;

namespace Scaffold.CloudCode
{
    /// <summary>
    /// Single place for Cloud Code failures: network/SDK, deserialization, <see cref="IRequestHandler{TResponse}.Validate"/>, etc.
    /// Applies to normal awaits and to optimistic trailing reconciliation. Add your logic here or subclass and register the subclass.
    /// </summary>
    public class CloudCodeErrorHandler
    {
        public virtual void Handle(Exception exception, string module, string endpoint, object requestPayload, object optimisticResponseOrNull)
        {
        }
    }
}
