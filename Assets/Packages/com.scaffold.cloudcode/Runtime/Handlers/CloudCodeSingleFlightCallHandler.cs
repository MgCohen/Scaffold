using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.CloudCode
{
    internal sealed class CloudCodeSingleFlightCallHandler : ICloudCodeCallHandler
    {
        internal CloudCodeSingleFlightCallHandler(ICloudCodeCallHandler inner)
        {
            this.inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
        }

        private readonly ICloudCodeCallHandler inner;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> moduleLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

        public async Task<string> InvokeAsync(string module, string endpoint, Dictionary<string, object> payload, CancellationToken cancellationToken)
        {
            SemaphoreSlim gate = moduleLocks.GetOrAdd(module, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await inner.InvokeAsync(module, endpoint, payload, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
