namespace Scaffold.CloudCode
{
    internal static class CloudCodeCallHandlerFactory
    {
        internal static ICloudCodeCallHandler CreateDefaultStack(CloudCodeSettings settings, ICloudCodeCallHandler baselineSdk)
        {
            ICloudCodeCallHandler inner = baselineSdk;
            inner = new CloudCodeTimeoutCallHandler(settings, inner);
            inner = new CloudCodeResponseBodyLoggingCallHandler(settings, inner);
            return new CloudCodeRetryCallHandler(settings, inner);
        }
    }
}
