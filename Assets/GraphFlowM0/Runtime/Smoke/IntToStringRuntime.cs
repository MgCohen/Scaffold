using System;
using System.Threading.Tasks;
using Scaffold.GraphFlow.M0;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>Pure data node — converts int to string via lazy <see cref="Connection{T}"/> reads.</summary>
    public sealed class IntToStringRuntime : RuntimeNode<MySmokeRunner>
    {
        public static class Ports
        {
            public const int InValue = unchecked((int)0xA001_0001u);
            public const int OutString = unchecked((int)0xA001_0002u);
        }

        [NonSerialized] Connection<int>? _inValue;

        public override Connection GetOutputConnection(int portId) => portId switch
        {
            Ports.OutString => new Connection<string>(
                this,
                Ports.OutString,
                () => (_inValue != null ? _inValue.Read() : 0).ToString()),
            _ => throw new ArgumentOutOfRangeException(nameof(portId)),
        };

        public override void BindInput(int portId, Connection connection)
        {
            switch (portId)
            {
                case Ports.InValue:
                    _inValue = (Connection<int>)connection;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(portId));
            }
        }

        public override Task<FlowContinuation> Execute(MySmokeRunner runner) =>
            Task.FromResult(FlowContinuation.Stop);
    }
}
