using LiveOps.Core.Initialize;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.Global
{
    public sealed class GlobalInstaller : GameModuleInstaller
    {
        public override void Install(ICloudCodeConfig config)
        {
            RegisterModule<GlobalConfigModule>(config);
        }
    }
}
