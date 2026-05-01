using LiveOps.GameApi;
using LiveOps.Initialize;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.Example
{
    /// <summary>Sample optional hook: register game-specific services (see <see cref="IGameSetup" />). Move to <c>LiveOps/Game</c> in real projects.</summary>
    public sealed class ExampleGameSetup : IGameSetup
    {
        public void Configure(ICloudCodeConfig config, GameApiRegistry registry)
        {
        }
    }
}
