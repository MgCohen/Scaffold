using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>Mode 2 smoke node — typed inheritance + BuildPayload / WriteOutputs.</summary>
    public sealed class EchoDispatcherRuntime : MyDispatcherBase<Echo, FakeResult>
    {
        public static class Ports
        {
            public const int FlowIn = unchecked((int)0xF003_0001u);
            public const int FlowOut = unchecked((int)0xF003_0002u);
            public const int Magnitude = unchecked((int)0xC003_0001u);
            public const int Summary = unchecked((int)0xC003_0002u);
        }

        public int Magnitude;

        [NonSerialized] Connection<int>? _in_Magnitude;
        [NonSerialized] public string _out_Summary = "";

        protected override int FlowOutPortId => Ports.FlowOut;

        protected override Echo BuildPayload() =>
            new Echo { Magnitude = _in_Magnitude != null ? _in_Magnitude.Read() : Magnitude };

        protected override void WriteOutputs(FakeResult result) => _out_Summary = result.Summary;

        protected override Task<FakeResult> DispatchAsync(MySmokeRunner runner, Echo cmd) =>
            Task.FromResult(new FakeResult { Summary = $"echo:{cmd.Magnitude}" });

        public override Connection GetOutputConnection(int portId) => portId switch
        {
            Ports.Summary => new Connection<string>(this, Ports.Summary, () => _out_Summary),
            _ => throw new ArgumentOutOfRangeException(nameof(portId)),
        };

        public override void BindInput(int portId, Connection connection)
        {
            switch (portId)
            {
                case Ports.Magnitude:
                    _in_Magnitude = (Connection<int>)connection;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(portId));
            }
        }
    }
}
