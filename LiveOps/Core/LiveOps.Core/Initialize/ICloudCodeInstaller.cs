using Unity.Services.CloudCode.Core;

namespace LiveOps.Core.Initialize
{
    /// <summary>
    /// Registers a slice of cloud DI (core vs feature modules).
    /// </summary>
    public interface ICloudCodeInstaller
    {
        void Install(ICloudCodeConfig config);
    }
}
