using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.CloudCode
{
    /// <summary>
    /// Chain link for Cloud Code module calls (delegating-handler style). Compose decorators outward from the SDK handler.
    /// </summary>
    internal interface ICloudCodeCallHandler
    {
        Task<string> InvokeAsync(string module, string endpoint, Dictionary<string, object> payload, CancellationToken cancellationToken);
    }
}
