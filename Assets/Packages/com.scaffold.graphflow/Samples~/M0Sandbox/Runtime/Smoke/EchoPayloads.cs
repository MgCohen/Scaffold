using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>Fake command/result pair — Card Framework-shaped smoke types. Discovered by the generator
    /// via the package's CommandBase = typeof(MyCommand&lt;&gt;); no [GraphCommandPair] needed.</summary>
    public sealed class Echo : MyCommand<FakeResult>, IGraphAction<MySmokeRunner>
    {
        [GraphPort]
        public int Magnitude;
    }

    public sealed class FakeResult
    {
        [GraphPort]
        public string Summary = "";
    }
}
