using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>Fake command/result pair — Card Framework-shaped smoke types.</summary>
    [GraphCommandPair(
        ResultType = typeof(FakeResult),
        FlowInPortId = unchecked((int)0xF003_0001u),
        FlowOutPortId = unchecked((int)0xF003_0002u))]
    public sealed class Echo : IGraphAction<MySmokeRunner>
    {
        [GraphPort(Id = unchecked((int)0xC003_0001u))]
        public int Magnitude;
    }

    public sealed class FakeResult
    {
        [GraphPort(Id = unchecked((int)0xC003_0002u))]
        public string Summary = "";
    }
}
