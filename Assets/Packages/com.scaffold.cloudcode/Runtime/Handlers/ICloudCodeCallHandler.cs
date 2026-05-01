using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.CloudCode
{
    internal interface ICloudCodeCallHandler
    {
        Task<string> InvokeAsync(string module, string endpoint, Dictionary<string, object> payload, CancellationToken cancellationToken);
    }
}
