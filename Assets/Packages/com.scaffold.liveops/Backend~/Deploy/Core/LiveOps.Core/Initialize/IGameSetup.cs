using LiveOps.GameApi;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Initialize
{
    /// <summary>
    /// Optional hook: register game-specific services after LiveOps module manifest registration. Implement
    /// in a consumer assembly under <c>LiveOps/Game</c>; do not add types under <c>LiveOps/Deploy</c>.
    /// </summary>
    public interface IGameSetup
    {
        void Configure(ICloudCodeConfig config, GameApiRegistry registry);
    }
}
