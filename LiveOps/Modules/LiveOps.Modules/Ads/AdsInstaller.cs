using LiveOps.Core.Initialize;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.Ads
{
    public sealed class AdsInstaller : GameModuleInstaller
    {
        public override void Install(ICloudCodeConfig config)
        {
            RegisterModule<AdsService>(config);
        }
    }
}
