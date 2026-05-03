using System.Threading.Tasks;

namespace Scaffold.GraphFlow.M0.Smoke
{
    public sealed partial class EchoDispatcherRuntime
    {
        protected override Task<FakeResult> DispatchAsync(MySmokeRunner runner, Echo cmd) =>
            Task.FromResult(new FakeResult { Summary = $"echo:{cmd.Magnitude}" });
    }
}
