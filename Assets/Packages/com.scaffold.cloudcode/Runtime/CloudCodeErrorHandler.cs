using System;

namespace Scaffold.CloudCode
{
    public class CloudCodeErrorHandler
    {
        public virtual void Handle(Exception exception, string module, string endpoint, object requestPayload, object optimisticResponseOrNull)
        {
        }
    }
}
