using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>Fake command/result pair — Card Framework-shaped smoke types.</summary>
    [GraphCommandPair(ResultType = typeof(FakeResult))]
    public sealed class Echo : IGraphAction<MySmokeRunner>
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
