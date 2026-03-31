using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.CloudCode
{
    public interface ICloudCodeModuleService
    {
        Task<T> CallEndpointAsync<T>(string module, string endpoint, int maxRetries = 2, int retryCall = 2, Dictionary<string, object> payload = null, CancellationToken cancellationToken = default);
    }
}
