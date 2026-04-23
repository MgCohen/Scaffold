using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.CloudCode
{
    internal sealed class CloudCodeResponseBodyLoggingCallHandler : ICloudCodeCallHandler
    {
        internal CloudCodeResponseBodyLoggingCallHandler(CloudCodeSettings settings, ICloudCodeCallHandler inner)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        private readonly CloudCodeSettings settings;
        private readonly ICloudCodeCallHandler inner;

        public async Task<string> InvokeAsync(string module, string endpoint, Dictionary<string, object> payload, CancellationToken cancellationToken)
        {
            string response = await inner.InvokeAsync(module, endpoint, payload, cancellationToken);
            if (settings.LogRawResponses)
            {
                Debug.Log($"[CloudCode] module '{module}' endpoint '{endpoint}' response: {response}");
            }

            return response;
        }
    }
}
