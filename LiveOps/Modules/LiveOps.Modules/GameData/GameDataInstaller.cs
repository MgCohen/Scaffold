using LiveOps.Core.Initialize;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.GameData
{
    /// <summary>
    /// Reference installer for the GameData flow. <see cref="GameDataHandler"/> is registered via
    /// CoreInstaller's IGameApiHandler scan of the LiveOps.Modules assembly.
    /// </summary>
    public sealed class GameDataInstaller : GameModuleInstaller
    {
        public override void Install(ICloudCodeConfig config)
        {
            // Intentionally empty: handler discovery lives in CoreInstaller.
        }
    }
}
