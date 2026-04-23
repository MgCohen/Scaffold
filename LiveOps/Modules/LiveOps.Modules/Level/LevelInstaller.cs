using LiveOps.Core.Initialize;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.Level
{
    public sealed class LevelInstaller : GameModuleInstaller
    {
        public override void Install(ICloudCodeConfig config)
        {
            RegisterModule<LevelService>(config);
        }
    }
}
