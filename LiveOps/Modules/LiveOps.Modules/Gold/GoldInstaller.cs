using LiveOps.Core.Initialize;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.Gold
{
    public sealed class GoldInstaller : GameModuleInstaller
    {
        public override void Install(ICloudCodeConfig config)
        {
            RegisterModule<GoldModule>(config);
        }
    }
}
