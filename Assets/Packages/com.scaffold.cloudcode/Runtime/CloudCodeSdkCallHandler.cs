using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SdkCloudCode = Unity.Services.CloudCode;

namespace Scaffold.CloudCode
{
    internal sealed class CloudCodeSdkCallHandler : ICloudCodeCallHandler
    {
        internal CloudCodeSdkCallHandler(SdkCloudCode.ICloudCodeService cloudCode)
        {
            this.cloudCode = cloudCode ?? throw new ArgumentNullException(nameof(cloudCode));
        }

        private readonly SdkCloudCode.ICloudCodeService cloudCode;

        public Task<string> InvokeAsync(string module, string endpoint, Dictionary<string, object> payload, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return cloudCode.CallModuleEndpointAsync(module, endpoint, payload);
        }
    }
}
