using LiveOps.Core.Initialize;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.DirectPush
{
    public sealed class DirectPushInstaller : GameModuleInstaller
    {
        public override void Install(ICloudCodeConfig config)
        {
            RegisterScoped<DirectPushService>(config);
        }
    }
}
