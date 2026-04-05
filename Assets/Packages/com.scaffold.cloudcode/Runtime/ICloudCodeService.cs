using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.CloudCode
{
    public interface ICloudCodeService
    {
        Task<T> CallEndpointAsync<T>(string module, string endpoint, object payload = null, CancellationToken cancellationToken = default);
    }
}
