using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.CloudCode
{
    internal sealed class CloudCodeTimeoutCallHandler : ICloudCodeCallHandler
    {
        internal CloudCodeTimeoutCallHandler(CloudCodeSettings settings, ICloudCodeCallHandler inner)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        private readonly CloudCodeSettings settings;
        private readonly ICloudCodeCallHandler inner;

        public async Task<string> InvokeAsync(string module, string endpoint, Dictionary<string, object> payload, CancellationToken cancellationToken)
        {
            if (settings.TimeoutMilliseconds <= 0)
            {
                return await inner.InvokeAsync(module, endpoint, payload, cancellationToken).ConfigureAwait(false);
            }

            Task<string> callTask = inner.InvokeAsync(module, endpoint, payload, cancellationToken);
            Task delayTask = Task.Delay(settings.TimeoutMilliseconds, cancellationToken);
            Task winner = await Task.WhenAny(callTask, delayTask).ConfigureAwait(false);
            if (winner == delayTask)
            {
                throw new TimeoutException($"Cloud Code call timed out after {settings.TimeoutMilliseconds} ms (module '{module}', endpoint '{endpoint}').");
            }

            return await callTask.ConfigureAwait(false);
        }
    }
}
